using Common;
using log4net;
using Microsoft.CodeAnalysis.CSharp;
using OfficeOpenXml.Style;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Services;

public class OrderService : IOrderService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly Context _context;
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Order> _orders = new();
    private readonly Dictionary<long, Order> _openOrders = new();
    private readonly Dictionary<long, Order> _ordersByExternalId = new();
    private readonly Dictionary<long, Order> _cancelledOrders = new();
    private readonly Dictionary<long, Order> _errorOrders = new();
    private readonly IdGenerator _orderIdGen;
    private readonly object _lock = new();

    public bool IsFakeOrderSupported => _execution.IsFakeOrderSupported;

    public event Action<Order>? AfterOrderSent;
    public event Action<Order>? OrderCancelled;
    public event Action<Order>? NextOrder;
    public event Action? OrderClosed;
    public event Action? OrderStoppedLost;
    public event Action? OrderTookProfit;
    public event Action? OrderSendingFailed;

    public OrderService(IExternalExecutionManagement execution,
                        Context context,
                        ISecurityService securityService,
                        Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _storage = context.Storage;
        _securityService = securityService;
        _persistence = persistence;

        _execution.OrderPlaced += OnSentOrderAccepted;
        _execution.OrderCancelled += OnOrderCancelled;
        _execution.OrderReceived += OnOrderReceived;

        _orderIdGen = new IdGenerator("OrderIdGen");
    }

    public Order? GetOrder(long orderId)
    {
        return _orders.ThreadSafeGet(orderId);
    }

    public Order? GetOrderByExternalId(long externalOrderId)
    {
        return _ordersByExternalId.ThreadSafeGet(externalOrderId);
    }

    public List<Order> GetOrders(Security? security = null, bool requestExternal = false)
    {
        // TODO
        return _orders.ThreadSafeValues();
    }

    public async Task<List<Order>> GetExternalOrders(Security security, DateTime start, DateTime? end = null)
    {
        var orders = new List<Order>();
        var state = await _execution.GetOrders(security, start: start, end: end);
        orders.AddOrAddRange(state.Get<List<Order>>(), state.Get<Order>());
        foreach (var order in orders)
        {
            order.AccountId = _context.AccountId;
        }
        Update(orders, security);
        return orders;
    }

    public async Task<List<Order>> GetStorageOrders(Security security, DateTime start, DateTime? end = null)
    {
        var orders = await _storage.ReadOrders(security, start, end ?? DateTime.UtcNow);
        Update(orders, security);
        return orders;
    }

    public List<Order> GetOrders(Security security, DateTime start, DateTime? end = null)
    {
        var e = end ?? DateTime.MaxValue;
        return _orders.ThreadSafeValues()
            .Where(o => o.SecurityId == security.Id && o.CreateTime <= start && o.UpdateTime >= e).ToList();
    }

    public async Task<List<Order>> GetExternalOpenOrders(Security? security = null)
    {
        if (security != null) Assertion.Shall(security.ExchangeType == _context.Exchange);
        var orders = new List<Order>();
        var state = await _execution.GetOpenOrders(security);
        orders.AddOrAddRange(state.Get<List<Order>>(), state.Get<Order>());
        Update(orders, security);
        return orders;
    }

    public async Task<List<Order>> GetStoredOpenOrders(Security? security = null)
    {
        if (security != null) Assertion.Shall(security.ExchangeType == _context.Exchange);

        var orders = await _storage.ReadOpenOrders(security);
        lock (_openOrders)
        {
            foreach (var openOrder in orders)
            {
                _openOrders[openOrder.Id] = openOrder;
            }
        }
        Update(orders, security);
        return orders;
    }

    public List<Order> GetOpenOrders(Security? security = null)
    {
        if (security != null) Assertion.Shall(security.ExchangeType == _context.Exchange);
        if (security == null)
            return _openOrders.ThreadSafeValues();

        return _openOrders.ThreadSafeValues().Where(o => o.SecurityId == security.Id).ToList();
    }

    public async Task<ExternalQueryState> SendOrder(Order order, bool isFakeOrder = false)
    {
        // this new order's id may or may not be used by external
        // eg. binance uses it
        if (order.Id <= 0)
            order.Id = _orderIdGen.NewTimeBasedId;
        if (order.CreateTime == DateTime.MinValue)
            order.CreateTime = DateTime.UtcNow;

        if (isFakeOrder && _execution is ISupportFakeOrder fakeOrderEndPoint)
        {
            return await fakeOrderEndPoint.SendFakeOrder(order);
        }
        else if (isFakeOrder)
        {
            var message = "The external end point does not support fake order.";
            _log.Warn(message + " Order will not be sent: " + order);
            return ExternalQueryStates.InvalidOrder("", "", message);
        }
        else
        {
            // persistence probably happens twice: one is before send (status = Sending)
            // the other is if order is accepted by external execution logic
            // and its new status (like Live / Filled) piggy-backed in the response
            _orders.ThreadSafeSet(order.Id, order);
            Persist(order);
            var state = await _execution.SendOrder(order);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                Assertion.Shall(order.ExternalOrderId > 0);
                _ordersByExternalId.ThreadSafeSet(order.ExternalOrderId, order);

                if (order.Status == OrderStatus.PartialFilled)
                    _openOrders.ThreadSafeSet(order.Id, order);

                Persist(order);
            }
            else if (state.ResultCode != ResultCode.SendOrderOk)
            {
                order.Status = OrderStatus.Failed;
                order.UpdateTime = DateTime.UtcNow;
                order.Comment += " | error code: " + state.ResultCode;
                Persist(order);
            }
            return state;
        }
    }

    public void CancelOrder(long orderId)
    {
        var order = GetOrder(orderId);
        if (order != null)
        {
            order.UpdateTime = DateTime.UtcNow;
            order.Status = OrderStatus.Canceling;

            _log.Info("Canceling order: " + order);
            var state = _execution.CancelOrder(order);
            // TODO
            Persist(order);
        }
    }

    public async Task CancelAllOpenOrders(Security security)
    {
        var state = await _execution.CancelAllOrders(security);
        _log.Info("Canceled all open orders: " + state);
        Persist(state);
    }

    public void Dispose()
    {
        _execution.OrderPlaced -= OnSentOrderAccepted;
        _execution.OrderCancelled -= OnOrderCancelled;
    }

    public Order CreateManualOrder(Security security,
                                   int accountId,
                                   decimal price,
                                   decimal quantity,
                                   Side side,
                                   OrderType orderType = OrderType.Limit,
                                   TimeInForceType timeInForce = TimeInForceType.GoodTillCancel,
                                   string comment = "manual")
    {
        var id = _orderIdGen.NewTimeBasedId;
        var now = DateTime.UtcNow;
        return new Order
        {
            Id = id,
            AccountId = accountId,
            CreateTime = now,
            UpdateTime = now,
            ExternalOrderId = id, // it maybe changed later by the exchange/broker
            Price = price,
            Quantity = quantity,
            Type = orderType,
            Security = security,
            SecurityId = security.Id,
            SecurityCode = security.Code,
            Side = side,
            Status = OrderStatus.Sending,
            StopPrice = 0,
            StrategyId = Consts.ManualTradingStrategyId,
            TimeInForce = timeInForce,
            Comment = comment,
        };
    }


    /// <summary>
    /// Receive an order message from external.
    /// </summary>
    /// <param name="order"></param>
    private void OnOrderReceived(Order order)
    {
        _log.Info("TEST--------- " + order);

        _securityService.Fix(order);

        var eoid = order.ExternalOrderId;
        var oid = order.Id;
        // already cached the order in SENDING state
        if (!_orders.ThreadSafeTryGet(oid, out var existingOrder))
        {
            throw Exceptions.Impossible("Before order is received it has been cached as a SENDING order");
        }
        else
        {
            order.Id = existingOrder.Id;
            order.AccountId = existingOrder.AccountId;
            order.CreateTime = existingOrder.CreateTime;
            _securityService.Fix(order);
            if (order.Status != existingOrder.Status)
            {
                if (existingOrder.IsClosed)
                {
                    // the incoming order is older than existing one
                    _log.Warn($"Out of sequence copy of order is received, id: {order.Id}; it will be ignored.");
                    return;
                }
                if (_log.IsDebugEnabled)
                    _log.Debug($"Order status is changed from {existingOrder.Status} to {order.Status}");
            }
            _orders.ThreadSafeSet(oid, order);
        }

        switch (order.Status)
        {
            case OrderStatus.Live or OrderStatus.PartialFilled:
                _openOrders.ThreadSafeSet(order.Id, order);
                break;
            case OrderStatus.Cancelled:
                _cancelledOrders.ThreadSafeSet(order.Id, order);
                _openOrders.ThreadSafeRemove(order.Id);
                break;
            case OrderStatus.Filled or OrderStatus.Expired:
                _openOrders.ThreadSafeRemove(order.Id);
                break;
            case OrderStatus.Unknown:
                break;
        }
        _ordersByExternalId.ThreadSafeSet(eoid, order);

        Persist(order);
        NextOrder?.Invoke(order);
    }

    private void OnSentOrderAccepted(bool isSuccessful, ExternalQueryState state)
    {
        if (!isSuccessful)
        {
            _log.Warn("Received a sent order action with issue.");
        }
        var order = state.Get<Order>();
        if (order == null)
        {
            _log.Warn("Received a state object without content!");
            return;
        }
        OnOrderReceived(order);
        AfterOrderSent?.Invoke(order);
    }

    private void OnOrderCancelled(bool isSuccessful, ExternalQueryState state)
    {
        if (!isSuccessful)
        {
            _log.Warn("Received a cancel order action with issue.");
        }

        var order = state.Get<Order>();
        if (order == null)
        {
            _log.Warn("Received a state object without content!");
            return;
        }

        lock (_lock)
        {
            _orders.Remove(order.Id);
            _cancelledOrders[order.Id] = order;
        }

        _log.Info("Cancelled order: " + order);

        Persist(order);
        OrderCancelled?.Invoke(order);
    }

    public void Update(ICollection<Order> orders, Security? security = null)
    {
        foreach (var order in orders)
        {
            if (order.Id <= 0)
            {
                // to avoid case that incoming orders are actually already cached even they don't have id assigned yet
                var sameOrder = _ordersByExternalId.GetOrDefault(order.ExternalOrderId);
                if (sameOrder != null)
                    order.Id = sameOrder.Id;
                else
                    order.Id = _orderIdGen.NewTimeBasedId;
            }
            order.AccountId = _context.AccountId;

            _securityService.Fix(order, security);

            if (order.Status is OrderStatus.Live or OrderStatus.PartialFilled)
            {
                _openOrders.ThreadSafeSet(order.Id, order);
            }
            else if (order.Status == OrderStatus.Failed)
            {
                _errorOrders.ThreadSafeSet(order.Id, order);
            }
            else if (order.Status is OrderStatus.Cancelled or OrderStatus.PartialCancelled)
            {
                _cancelledOrders.ThreadSafeSet(order.Id, order);
            }
            _orders.ThreadSafeSet(order.Id, order);
            _ordersByExternalId.ThreadSafeSet(order.ExternalOrderId, order);
        }
    }

    public void Persist(Order order)
    {
        _securityService.Fix(order);
        _persistence.Insert(order);
    }

    private void Persist(ExternalQueryState state)
    {
        if (state.ResultCode == ResultCode.CancelOrderOk)
        {
            var orders = state.Get<List<Order>>();
            var order = state.Get<Order>();
            // expect to have at least one
            if (!orders.IsNullOrEmpty())
            {
                // it is possible that the orders have different security
                // so need to persist one by one
                foreach (var o in orders)
                {
                    Persist(o);
                }
            }
            if (order != null)
            {
                Persist(order);
            }
        }
    }

    public void ClearCachedClosedPositionOrders(Position? position = null)
    {
        if (_orders.IsNullOrEmpty()) return;

        if (position != null && position.IsClosed)
        {
            lock (_orders)
            {
                var orders = _orders.Values.Where(o => o.SecurityId == position.SecurityId).ToList();
                if (orders.IsNullOrEmpty()) return;

                var security = orders[0].Security;
                var trades = AsyncHelper.RunSync(() => _context.Storage.ReadTrades(security, orders[0].CreateTime, DateTime.MaxValue));
                var closedOrderIds = trades.Where(t => position.Id == t.PositionId).Select(t => t.OrderId);
                Clear(closedOrderIds);
            }
        }
        else if (position == null)
        {
            var start = _orders.Values.Min(o => o.UpdateTime);
            var positions = AsyncHelper.RunSync(() => _context.Storage.ReadPositions(_context.Account, start))
                .Where(p => p.IsClosed).ToList();
            var groupedOrders = _orders.Values.GroupBy(o => o.Security);
            foreach (var group in groupedOrders)
            {
                var security = group.Key;
                var trades = AsyncHelper.RunSync(() => _context.Storage.ReadTrades(security, start, DateTime.MaxValue));
                var closedOrderIds = trades.Where(t => positions.Any(p => p.Id == t.PositionId)).Select(t => t.OrderId);
                Clear(closedOrderIds);
            }
        }

        void Clear(IEnumerable<long> closedOrderIds)
        {
            lock (_orders)
            {
                foreach (var id in closedOrderIds)
                {
                    var order = _orders.GetOrDefault(id);
                    _orders.ThreadSafeRemove(id);
                    if (order != null)
                        _ordersByExternalId.ThreadSafeRemove(order.ExternalOrderId);
                    _cancelledOrders.ThreadSafeRemove(id);
                    _errorOrders.ThreadSafeRemove(id);
                }
            }
        }
    }
}
