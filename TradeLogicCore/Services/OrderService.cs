﻿using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
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

    public async Task<List<Order>> GetOrders(Security security, DateTime start, DateTime end, bool requestExternal = false)
    {
        var orders = new List<Order>();
        if (requestExternal)
        {
            var state = await _execution.GetOrderHistory(security, start, end);
            orders.AddOrAddRange(state.Get<List<Order>>(), state.Get<Order>());
            foreach (var order in orders)
            {
                order.AccountId = _context.Account!.Id;
                order.BrokerId = _context.BrokerId;
                order.ExchangeId = _context.ExchangeId;
                order.SecurityCode = security.Code;
            }
        }
        else
        {
            orders = await _storage.ReadOrders(security, start, end);
        }
        foreach (var order in orders)
        {
            order.SecurityCode = security.Code;
        }
        return orders;
    }

    public async Task<List<Order>> GetOpenOrders(Security? security = null, bool requestExternal = false)
    {
        if (security != null)
            Assertion.Shall(ExchangeTypeConverter.Parse(security.Exchange) == _context.Exchange);

        if (requestExternal)
        {
            var state = await _execution.GetOpenOrders(security);
            var orders = state.Get<List<Order>>();
            if (orders == null)
            {
                var order = state.Get<Order>();
                return order == null ? new List<Order>() : new List<Order> { order };
            }
            return orders;
        }
        else
        {
            return _openOrders.IsNullOrEmpty() ? await _storage.ReadOpenOrders(security) : _openOrders.ThreadSafeValues();
        }
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
            Persist(order);
            var state = await _execution.SendOrder(order);
            _ordersByExternalId.ThreadSafeSet(order.ExternalOrderId, order);
            _log.Info("Sent a new order: " + order);

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

            _execution.CancelOrder(order);
            _log.Info("Canceling order: " + order);
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
                                   TimeInForceType timeInForce = TimeInForceType.GoodTillCancel)
    {
        var id = _orderIdGen.NewTimeBasedId;
        var now = DateTime.UtcNow;
        return new Order
        {
            Id = id,
            AccountId = accountId,
            BrokerId = _context.BrokerId,
            CreateTime = now,
            UpdateTime = now,
            ExchangeId = ExchangeIds.GetId(security.Exchange),
            ExternalOrderId = id, // it maybe changed later by the exchange/broker
            Price = price,
            Quantity = quantity,
            Type = orderType,
            SecurityCode = security.Code,
            SecurityId = security.Id,
            Side = side,
            Status = OrderStatus.Sending,
            StopPrice = 0,
            StrategyId = Consts.ManualTradingStrategyId,
            TimeInForce = timeInForce,
        };
    }


    /// <summary>
    /// Receive an order message from external.
    /// </summary>
    /// <param name="order"></param>
    private void OnOrderReceived(Order order)
    {
        var eoid = order.ExternalOrderId;
        var oid = order.Id;
        if (!_orders.ThreadSafeTryGet(oid, out var existingOrder))
        {
            // TODO
        }
        else
        {
            order.Id = existingOrder.Id;
            order.AccountId = existingOrder.AccountId;
            order.CreateTime = existingOrder.CreateTime;
            order.SecurityId = existingOrder.SecurityId;
            if (order.Status != existingOrder.Status)
            {
                _log.Debug($"Order status is changed from {existingOrder.Status} to {order.Status}");
            }
            _orders.ThreadSafeSet(oid, order);
        }

        if (order.Status is OrderStatus.Live or OrderStatus.PartialFilled)
        {
            _openOrders.ThreadSafeSet(order.Id, order);
            _ordersByExternalId.ThreadSafeSet(eoid, order);
        }

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

        _orders.ThreadSafeSet(order.Id, order, _lock);
        _ordersByExternalId.ThreadSafeSet(order.ExternalOrderId, order, _lock);

        _log.Info("Sent order: " + order);

        Persist(order);
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

    public void Update(ICollection<Order> orders)
    {
        foreach (var order in orders)
        {
            if (order.Id <= 0)
            {
                order.Id = _orderIdGen.NewTimeBasedId;
            }
            order.AccountId = _context.Account.Id;
            order.BrokerId = _context.BrokerId;
            order.ExchangeId = _context.ExchangeId;
            if (order.Status == OrderStatus.Live)
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
        _persistence.Enqueue(new PersistenceTask<Order>(order) { ActionType = DatabaseActionType.Update });
    }

    private void Persist(ExternalQueryState state)
    {
        if (state.ResultCode == ResultCode.CancelOrderOk)
        {
            var orders = state.Get<List<Order>>();
            var order = state.Get<Order>();
            // expect to have at least one
            if (!orders.IsNullOrEmpty())
                _persistence.Enqueue(new PersistenceTask<Order>(orders) { ActionType = DatabaseActionType.Update });
            if (order != null)
                _persistence.Enqueue(new PersistenceTask<Order>(order) { ActionType = DatabaseActionType.Update });
        }
    }
}
