using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
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
    private readonly Dictionary<long, Order> _allOrders = new(); // all orders
    private readonly Dictionary<long, Order> _allOrdersByExternalId = new(); // same as _allOrders
    private readonly Dictionary<long, Order> _openOrders = new(); // all live, partial filled orders
    private readonly Dictionary<long, Order> _cancelledOrders = new(); // all cancelled, expired orders
    private readonly Dictionary<long, Order> _errorOrders = new(); // all error, rejected orders
    private readonly Dictionary<long, Order> _operationalOrders = new(); // orders which should not be considered affecting any positions
    private readonly Dictionary<long, List<OrderState>> _orderStates = new(); // full states for all orders
    private readonly IdGenerator _orderIdGen;
    private readonly object _lock = new();

    public event Action<Order>? AfterOrderSent;
    public event Action<Order>? OrderCancelled;
    public event Action? OrderClosed;
    public event Action? OrderStoppedLost;
    public event Action? OrderTookProfit;
    public event Action? OrderSendingFailed;

    public event Action<Order>? OrderProcessed;

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
        return _allOrders.ThreadSafeGet(orderId);
    }

    public Order? GetOrderByExternalId(long externalOrderId)
    {
        return _allOrdersByExternalId.ThreadSafeGet(externalOrderId);
    }

    public async Task<List<Order>> GetExternalOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses)
    {
        var orders = new List<Order>();
        var state = await _execution.GetOrders(security, start: start, end: end);
        orders.AddOrAddRange(state.Get<List<Order>>(), state.Get<Order>());
        foreach (var order in orders)
        {
            order.AccountId = _context.AccountId;
        }
        Update(orders, security);

        return statuses.IsNullOrEmpty() ? orders : orders.Where(o => statuses.Contains(o.Status)).ToList();
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

    public async Task<List<Order>> GetStorageOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses)
    {
        var orders = await _storage.ReadOrders(security, null, start, end, statuses);
        Update(orders, security);
        return orders;
    }

    public List<Order> GetOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses)
    {
        var e = end ?? DateTime.MaxValue;
        return _allOrders.ThreadSafeValues()
            .Where(o => o.SecurityId == security.Id && o.CreateTime <= start && o.UpdateTime >= e && statuses.Contains(o.Status))
            .ToList();
    }

    public List<Order> GetOpenOrders(Security? security = null)
    {
        if (security != null) Assertion.Shall(security.ExchangeType == _context.Exchange);
        return security == null
            ? _openOrders.ThreadSafeValues()
            : _openOrders.ThreadSafeValues().Where(o => o.SecurityId == security.Id).ToList();
    }

    public async Task<List<OrderState>> GetOrderStates(Security security, DateTime start, DateTime? end = null)
    {
        var orderStates = await _storage.ReadOrderStates(security, start, end ?? DateTime.UtcNow);
        Update(orderStates, security);
        return orderStates;
    }

    public async Task<List<Order>> GetOrderJoinedStates(Security security, DateTime start, DateTime? end = null)
    {
        var orders = await _storage.ReadOrderJoinedStates(security, start, end ?? DateTime.UtcNow);
        _securityService.Fix(orders);
        return orders;
    }

    public async Task<ExternalQueryState> SendOrder(Order order)
    {
        // this new order's id may or may not be used by external
        // eg. binance uses it
        if (order.Id <= 0)
            order.Id = _orderIdGen.NewTimeBasedId;
        if (order.CreateTime == DateTime.MinValue)
            order.CreateTime = DateTime.UtcNow;

        _securityService.Fix(order);

        // persistence probably happens twice: one is before send (status = Sending)
        // the other is if order is accepted by external execution logic
        // and its new status (like Live / Filled) piggy-backed in the response
        // or the order failed to be sent, status = Failed.
        _allOrders.ThreadSafeSet(order.Id, order);
        _persistence.Insert(order);

        var isOperational = order.Action == OrderActionType.Operational;
        if (isOperational)
            _operationalOrders.ThreadSafeSet(order.Id, order);
        var action = order.Action;
        var state = await _execution.SendOrder(order);
        if (state.ResultCode == ResultCode.SendOrderOk)
        {
            order = state.Get<Order>()!;
            Assertion.Shall(order.ExternalOrderId > 0);
            _allOrdersByExternalId.ThreadSafeSet(order.ExternalOrderId, order);

            if (order.Status is OrderStatus.Live or OrderStatus.PartialFilled)
                _openOrders.ThreadSafeSet(order.Id, order);
        }
        else if (state.ResultCode != ResultCode.SendOrderOk)
        {
            order.Status = OrderStatus.Failed;
            order.UpdateTime = DateTime.UtcNow;
            order.Comment += " | error code: " + state.ResultCode;

            _allOrders.ThreadSafeRemove(order.Id);
            _errorOrders.ThreadSafeSet(order.Id, order);
        }
        order.Action = action;
        _persistence.Insert(order);
        return state;
    }

    public bool IsOperational(long orderId)
    {
        return _operationalOrders.ThreadSafeContains(orderId);
    }

    public async Task<bool> CancelOrder(Order order)
    {
        if (order.IsClosed)
            return false;

        order.UpdateTime = DateTime.UtcNow;
        order.Status = OrderStatus.Canceling;

        var state = await _execution.CancelOrder(order);
        var externalOrder = state.Get<Order>();
        if (state.ResultCode == ResultCode.CancelOrderOk && externalOrder != null)
        {
            _log.Info($"Canceled order: " + externalOrder.Id);

            await FixOrderIdByExternalId(order);
            SyncCachedCancelledOrder(order);
            _persistence.Insert(order);

            return true;
        }
        else
        {
            _log.Error($"Failed to cancel order {order.Id}! ResultCode: {state.ResultCode}; description: {state.Description}");

            order.Status = OrderStatus.Failed;
            order.UpdateTime = DateTime.UtcNow;
            order.Comment += " | error code: " + state.ResultCode;
            _persistence.Insert(order);
            return false;
        }
    }

    public async Task<bool> CancelAllOpenOrders(Security security, OrderActionType action, bool syncExternal)
    {
        List<Order>? openOrders;
        if (syncExternal)
        {
            var state = await _execution.GetOpenOrders(security);
            if (state.ResultCode != ResultCode.GetOrderOk)
            {
                _log.Error("Failed to query opened orders for security " + security.Code);
                return false;
            }
            openOrders = state.Get<List<Order>>();
        }
        else
        {
            openOrders = _openOrders.ThreadSafeValues();
        }
        if (!openOrders.IsNullOrEmpty())
        {
            _securityService.Fix(openOrders);
            // orders from external has no internal id
            foreach (var order in openOrders)
            {
                await FixOrderIdByExternalId(order);
            }

            var state = await _execution.CancelAllOrders(security);
            var cancelledOrders = state.Get<List<Order>>();
            if (state.ResultCode == ResultCode.CancelOrderOk && !cancelledOrders.IsNullOrEmpty())
            {
                _log.Info($"Cancelled {cancelledOrders.Count} open orders.");
                _securityService.Fix(cancelledOrders);
                foreach (var order in cancelledOrders)
                {
                    order.Action = action;
                    await FixOrderIdByExternalId(order);
                    SyncCachedCancelledOrder(order);
                }
                _persistence.Insert(cancelledOrders);
            }
            else
            {
                _log.Error($"Failed to cancel orders! ResultCode: {state.ResultCode}; description: {state.Description}");
                return false;
            }
        }
        return true;
    }

    public async Task<bool> CancelAllOpenOrders()
    {
        var state0 = await _execution.GetOpenOrders();
        if (state0.ResultCode != ResultCode.GetOrderOk)
        {
            _log.Error("Failed to query opened orders before cancellation.");
            return false;
        }
        var openOrders = state0.Get<List<Order>>();
        if (openOrders.IsNullOrEmpty())
            return false;

        _securityService.Fix(openOrders);
        // orders from external has no internal id
        var securities = openOrders.Select(o => o.Security).Distinct().ToList();
        var isGood = true;
        foreach (var security in securities)
        {
            try
            {
                var state1 = await _execution.CancelAllOrders(security);
                var cancelled = state1.Get<List<Order>>();
                if (state1.ResultCode == ResultCode.CancelOrderOk && !cancelled.IsNullOrEmpty())
                {
                    _log.Info($"Cancelled {cancelled.Count} open orders.");
                    _persistence.Insert(cancelled);
                    foreach (var o in cancelled)
                    {
                        SyncCachedCancelledOrder(o);
                    }
                }
                else
                {
                    _log.Error($"Failed to cancel orders for {security.Code}! ResultCode: {state1.ResultCode}; description: {state1.Description}. Continue to cancel remaining securities if any.");
                    isGood = false;
                }
            }
            catch (Exception e)
            {
                _log.Error($"Failed to cancel orders for {security.Code}! Continue to cancel remaining securities if any.", e);
                isGood = false;
            }
        }
        return isGood;
    }

    private async Task FixOrderIdByExternalId(Order order)
    {
        if (order.Id <= 0)
        {
            var existingOpenOrder = _allOrdersByExternalId.GetOrDefault(order.ExternalOrderId);
            if (existingOpenOrder == null)
                existingOpenOrder = _openOrders.ThreadSafeValues().FirstOrDefault(o => o.ExternalOrderId == order.ExternalOrderId);
            if (existingOpenOrder == null)
                existingOpenOrder = _cancelledOrders.ThreadSafeValues().FirstOrDefault(o => o.ExternalOrderId == order.ExternalOrderId);
            if (existingOpenOrder == null)
                existingOpenOrder = _errorOrders.ThreadSafeValues().FirstOrDefault(o => o.ExternalOrderId == order.ExternalOrderId);
            if (existingOpenOrder != null)
            {
                order.Id = existingOpenOrder.Id;
                return;
            }

            // rare case: fallback to storage
            var internalOrder = await _storage.ReadOrderByExternalId(order.ExternalOrderId);
            if (internalOrder != null)
            {
                order.Id = internalOrder.Id;
                return;
            }

            // it must be a missing order which was not stored in storage
            _log.Warn("No cached open order found! External open order id: " + order.ExternalOrderId);
            _log.Warn("Now creating an order entry for it...");
            order.Id = _orderIdGen.NewTimeBasedId;
            order.AccountId = _context.AccountId;
            order.Comment = "Not-sync order discovered during cancellation of all open orders.";
            _persistence.Insert(order);
            _persistence.WaitAll(); // to prevent onOrderReceived event bringing in an order update without anything cached
        }
    }

    public void Dispose()
    {
        _execution.OrderPlaced -= OnSentOrderAccepted;
        _execution.OrderCancelled -= OnOrderCancelled;
    }

    public Order CreateManualOrder(Security security,
                                   decimal price,
                                   decimal quantity,
                                   Side side,
                                   OrderType orderType = OrderType.Limit,
                                   string comment = "manual",
                                   TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    {
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            AccountId = _context.AccountId,
            CreateTime = now,
            UpdateTime = now,
            ExternalOrderId = _orderIdGen.NewNegativeTimeBasedId, // we may have multiple SENDING orders coexist
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
        return order;
    }

    //public async Task<bool> SendLongMarketOrder(string securityCode,
    //                                            decimal quantity,
    //                                            string comment = "",
    //                                            TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    //{
    //    var security = _securityService.GetSecurity(securityCode);
    //    if (security == null) return false;
    //    var order = CreateManualOrder(security, 0, quantity, Side.Buy, OrderType.Market, comment, timeInForce);
    //    var state = await SendOrder(order);
    //    return state.ResultCode == ResultCode.SendOrderOk;
    //}

    //public async Task<bool> SendShortMarketOrder(string securityCode,
    //                                             decimal quantity,
    //                                             string comment = "",
    //                                             TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    //{
    //    var security = _securityService.GetSecurity(securityCode);
    //    if (security == null) return false;
    //    var order = CreateManualOrder(security, 0, quantity, Side.Sell, OrderType.Market, comment, timeInForce);
    //    var state = await SendOrder(order);
    //    return state.ResultCode == ResultCode.SendOrderOk;
    //}

    //public async Task<bool> SendLongLimitOrder(string securityCode,
    //                                           decimal price,
    //                                           decimal quantity,
    //                                           string comment = "",
    //                                           TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    //{
    //    var security = _securityService.GetSecurity(securityCode);
    //    if (security == null) return false;
    //    var order = CreateManualOrder(security, price, quantity, Side.Buy, OrderType.Limit, comment, timeInForce);
    //    var state = await SendOrder(order);
    //    return state.ResultCode == ResultCode.SendOrderOk;
    //}

    //public async Task<bool> SendShortLimitOrder(string securityCode,
    //                                            decimal price,
    //                                            decimal quantity,
    //                                            string comment = "",
    //                                            TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    //{
    //    var security = _securityService.GetSecurity(securityCode);
    //    if (security == null) return false;
    //    var order = CreateManualOrder(security, price, quantity, Side.Sell, OrderType.Limit, comment, timeInForce);
    //    var state = await SendOrder(order);
    //    return state.ResultCode == ResultCode.SendOrderOk;
    //}

    public void Reset()
    {
        _errorOrders.Clear();
        _cancelledOrders.Clear();
        _openOrders.Clear();
        _allOrders.Clear();
        _allOrdersByExternalId.Clear();
    }

    public void Update(ICollection<Order> orders, Security? security = null)
    {
        foreach (var order in orders)
        {
            if (order.Id <= 0)
            {
                // to avoid case that incoming orders are actually already cached even they don't have id assigned yet
                var sameOrder = _allOrdersByExternalId.GetOrDefault(order.ExternalOrderId);
                order.Id = sameOrder != null ? sameOrder.Id : _orderIdGen.NewTimeBasedId;
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
            _allOrders.ThreadSafeSet(order.Id, order);
            _allOrdersByExternalId.ThreadSafeSet(order.ExternalOrderId, order);
        }
    }

    public void Update(ICollection<OrderState> orderStates, Security? security = null)
    {
        foreach (var orderState in orderStates)
        {
            _securityService.Fix(orderState, security);
        }
    }

    public void ClearCachedClosedPositionOrders(Position? position = null)
    {
        if (_allOrders.IsNullOrEmpty()) return;

        if (position != null && position.IsClosed)
        {
            lock (_allOrders)
            {
                var orders = _allOrders.Values.Where(o => o.SecurityId == position.SecurityId).ToList();
                if (orders.IsNullOrEmpty()) return;

                var security = orders[0].Security;
                var trades = AsyncHelper.RunSync(() => _context.Storage.ReadTrades(security, orders[0].CreateTime, DateTime.MaxValue));
                var closedOrderIds = trades.Where(t => position.Id == t.PositionId).Select(t => t.OrderId);
                Clear(closedOrderIds);
            }
        }
        else if (position == null)
        {
            var start = _allOrders.Values.Min(o => o.UpdateTime);
            var positions = AsyncHelper.RunSync(() => _context.Services.Portfolio.GetStoragePositions(start, OpenClose.ClosedOnly)).ToList();
            var groupedOrders = _allOrders.Values.GroupBy(o => o.Security);
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
            lock (_allOrders)
            {
                foreach (var id in closedOrderIds)
                {
                    var order = _allOrders.GetOrDefault(id);
                    _allOrders.ThreadSafeRemove(id);
                    if (order != null)
                        _allOrdersByExternalId.ThreadSafeRemove(order.ExternalOrderId);
                    _cancelledOrders.ThreadSafeRemove(id);
                    _errorOrders.ThreadSafeRemove(id);
                }
            }
        }
    }

    /// <summary>
    /// Receive an order message from external.
    /// </summary>
    /// <param name="order"></param>
    private async void OnOrderReceived(Order order)
    {
        _securityService.Fix(order);

        var eoid = order.ExternalOrderId;
        var oid = order.Id;

        if (_allOrders.ThreadSafeTryGet(oid, out var existingOrder) || _allOrdersByExternalId.ThreadSafeTryGet(eoid, out existingOrder))
        {
            // already cached the order in SENDING state
            if (order.FilledQuantity < existingOrder.FilledQuantity)
            {
                // must ignore this out of order piece of info
                _log.Warn("Received an order which filled quantity is smaller than the one cached, id: " + order.Id);
                return;
            }
            if (order.UpdateTime < existingOrder.UpdateTime)
            {
                // must ignore this out of order piece of info
                _log.Warn("Received an order which update time is smaller than the one cached, id: " + order.Id);
                return;
            }
            if (order.Status == existingOrder.Status && order.FilledQuantity == existingOrder.FilledQuantity)
            {
                // can ignore this duplicated piece of info
                _log.Warn("Received an order which has the same status and filled quantity, id: " + order.Id);
                return;
            }

            order.Id = existingOrder.Id;
            order.AccountId = existingOrder.AccountId;
            order.CreateTime = existingOrder.CreateTime;
            order.Comment = existingOrder.Comment;
            order.ParentOrderId = existingOrder.ParentOrderId;

            if (!order.Price.IsValid() || order.Price == 0)
            {
                order.Price = existingOrder.Price != 0 ? existingOrder.Price : 0;
            }
            if (order.Status != existingOrder.Status)
            {
                if (existingOrder.IsClosed)
                {
                    // the incoming order is older than existing one
                    if (_log.IsDebugEnabled)
                        _log.Debug($"Out of sequence copy of order is received, id: {order.Id}; it will be ignored.");
                    return;
                }
                if (order.Status == OrderStatus.Unknown && existingOrder.Status != OrderStatus.Unknown)
                {
                    // the incoming order is less 'valid'
                    _log.Warn($"Unknown status order is received, id: {order.Id}; it will be ignored.");
                    return;
                }
                if (_log.IsDebugEnabled)
                    _log.Debug($"Order status is changed from {existingOrder.Status} to {order.Status}");
            }
        }
        else
        {
            // probably an order which was not saved in database due to program crash, and being cancelled during startup
            _log.Warn("Received an order which was not cached.");

            await FixOrderIdByExternalId(order);
        }

        _allOrders.ThreadSafeSet(order.Id, order);
        switch (order.Status)
        {
            case OrderStatus.Live or OrderStatus.PartialFilled:
                _openOrders.ThreadSafeSet(order.Id, order);
                break;
            case OrderStatus.Cancelled:
                _cancelledOrders.ThreadSafeSet(order.Id, order);
                break;
        }
        if (order.Status.IsFinished())
            _openOrders.ThreadSafeRemove(order.Id);

        _allOrdersByExternalId.ThreadSafeSet(eoid, order);
        _persistence.Insert(order);

        // cache and store the state
        var state = OrderState.From(order);
        var states = _orderStates.ThreadSafeGetOrCreate(state.OrderId);
        states.ThreadSafeAdd(state);
        _persistence.Insert(state, isUpsert: false);

        OrderProcessed?.Invoke(order);
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
            _cancelledOrders[order.Id] = order;
        }

        _log.Info("Cancelled order: " + order);

        _persistence.Insert(order);
        OrderCancelled?.Invoke(order);
    }

    private void SyncCachedCancelledOrder(Order order)
    {
        _openOrders.ThreadSafeRemove(order.Id);
        _cancelledOrders.ThreadSafeSet(order.Id, order);
        _allOrdersByExternalId.ThreadSafeRemove(order.ExternalOrderId);
    }
}
