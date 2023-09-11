﻿using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Services;

public class OrderService : IOrderService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly Context _context;
    private readonly ISecurityService _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Order> _orders = new();
    private readonly Dictionary<long, Order> _externalOrderIdToOrders = new();
    private readonly Dictionary<long, Order> _cancelledOrders = new();
    private readonly IdGenerator _idGenerator;
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
        _securityService = securityService;
        _persistence = persistence;

        _execution.OrderPlaced += OnSentOrderAccepted;
        _execution.OrderCancelled += OnOrderCancelled;
        _execution.OrderReceived += OnOrderReceived;

        _idGenerator = new IdGenerator("OrderIdGen");
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
            _externalOrderIdToOrders.ThreadSafeSet(eoid, order);
            _orders.ThreadSafeSet(oid, order);
            _persistence.Enqueue(new PersistenceTask<Order>(order) { ActionType = DatabaseActionType.Update });
        }
        NextOrder?.Invoke(order);
    }

    public Order? GetOrder(long orderId)
    {
        return _orders.ThreadSafeTryGet(orderId, out var order) ? order : null;
    }

    public Order? GetOrderByExternalId(long externalOrderId)
    {
        return _externalOrderIdToOrders.ThreadSafeTryGet(externalOrderId, out var order) ? order : null;
    }

    public async Task<Order[]> GetOrderHistory(DateTime start, DateTime end, Security security, bool requestExternal = false)
    {
        Order[] orders;
        if (requestExternal)
        {
            var state = await _execution.GetOrderHistory(security, start, end);
            orders = state?.ContentAs<Order[]>() ?? new Order[0];
        }
        else
        {
            orders = (await Storage.ReadOrders(security, start, end)).ToArray();
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
            return state.ContentAs<List<Order>?>() ?? new List<Order>();
        }
        else
        {
            return await Storage.ReadOpenOrders(security);
        }
    }

    public void SendOrder(Order order, bool isFakeOrder = true)
    {
        // this new order's id may or may not be used by external
        // eg. binance uses it
        if (order.Id == 0)
            order.Id = _idGenerator.NewTimeBasedId;
        if (order.CreateTime == DateTime.MinValue)
            order.CreateTime = DateTime.UtcNow;
        order.ExternalOrderId = order.Id.ReverseDigits();
        if (isFakeOrder && _execution is ISupportFakeOrder fakeOrderEndPoint)
        {
            fakeOrderEndPoint.SendFakeOrder(order);
        }
        else if (isFakeOrder)
        {
            _log.Warn("The external end point does not support fake order. Order will not be sent: " + order);
            return;
        }
        else
        {
            _execution.SendOrder(order);
        }

        _log.Info("Sent a new order: " + order);
        Persist(order);
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

    public void CancelAllOpenOrders()
    {
        var securityIds = _orders.Values.Where(o => o.Status is OrderStatus.Live or OrderStatus.PartialFilled or OrderStatus.PartialCancelled)
            .Select(o => o.SecurityId).ToList();
        Task.Run(async () =>
        {
            var securities = await _securityService.GetSecurities(securityIds);
            if (securities == null)
                return;
            foreach (var security in securities)
            {
                await _execution.CancelAllOrders(security);
            }
        });
        _log.Info("Canceling all open orders.");
    }

    private void OnSentOrderAccepted(bool isSuccessful, ExternalQueryState state)
    {
        if (!isSuccessful)
        {
            _log.Warn("Received a sent order action with issue.");
        }

        var order = state.ContentAs<Order>();
        if (order == null)
        {
            _log.Warn("Received a state object without content!");
            return;
        }

        lock (_lock)
            _orders[order.Id] = order;

        _log.Info("Sent order: " + order);

        AfterOrderSent?.Invoke(order);
        Persist(order);
    }

    private void OnOrderCancelled(bool isSuccessful, ExternalQueryState state)
    {
        if (!isSuccessful)
        {
            _log.Warn("Received a cancel order action with issue.");
        }

        var order = state.ContentAs<Order>();
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

        OrderCancelled?.Invoke(order);
        Persist(order);
    }

    private void Persist(Order order)
    {
        var orderTask = new PersistenceTask<Order>(order);
        _persistence.Enqueue(orderTask);
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
        var id = _idGenerator.NewTimeBasedId;
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
            Status = OrderStatus.Placing,
            StopPrice = 0,
            StrategyId = Constants.ManualTradingStrategyId,
            TimeInForce = timeInForce,
        };
    }

    public void CloseAllOpenPositions()
    {
        throw new NotImplementedException();
    }
}
