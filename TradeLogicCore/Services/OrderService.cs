﻿using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;

namespace TradeLogicCore.Services;

public class OrderService : IOrderService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Order> _orders = new();
    private readonly Dictionary<long, Order> _externalOrderIdToOrders = new();
    private readonly Dictionary<long, Order> _cancelledOrders = new();
    private readonly IdGenerator _idGenerator;
    private readonly object _lock = new();

    public event Action<Order>? OrderAcknowledged;
    public event Action<Order>? OrderCancelled;

    public OrderService(IExternalExecutionManagement execution, Persistence persistence)
    {
        _execution = execution;
        _persistence = persistence;

        _execution.OrderPlaced += OnSentOrderAccepted;
        _execution.OrderCanceled += OnOrderCancelled;

        _idGenerator = new IdGenerator();
    }

    public Order? GetOrder(long orderId)
    {
        return _orders.TryGetValue(orderId, out var order) ? order : null;
    }

    public Order? GetOrderByExternalId(long externalOrderId)
    {
        throw new NotImplementedException();
    }

    public void SendOrder(Order order)
    {
        // this new order's id may or may not be used by external
        // eg. binance uses it
        order.ExternalOrderId = _idGenerator.NewTimeBasedId;
        _execution.SendOrder(order);
        _log.Info("Sending order: " + order);
    }

    public void CancelOrder(long orderId)
    {
        var order = GetOrder(orderId);
        if (order != null)
        {
            _execution.CancelOrder(order);
            _log.Info("Canceling order: " + order);
        }
    }

    private void OnSentOrderAccepted(bool isSuccessful, Order order)
    {
        lock (_lock)
            _orders[order.Id] = order;

        _log.Info("Sent order: " + order);

        OrderAcknowledged?.Invoke(order);
        Persist(order);
    }

    private void OnOrderCancelled(bool isSuccessful, Order order)
    {
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
        var orderTask = new PersistenceTask<Order>()
        {
            Entry = order,
            DatabaseName = DatabaseNames.ExecutionData
        };
        _persistence.Enqueue(orderTask);
    }

    public void Dispose()
    {
        _execution.OrderPlaced -= OnSentOrderAccepted;
        _execution.OrderCanceled -= OnOrderCancelled;
    }
}
