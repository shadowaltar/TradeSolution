using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
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

    public bool IsFakeOrderSupported => _execution.IsFakeOrderSupported;

    public event Action<Order>? OrderAcknowledged;
    public event Action<Order>? OrderCancelled;

    public OrderService(IExternalExecutionManagement execution, Persistence persistence)
    {
        _execution = execution;
        _persistence = persistence;

        _execution.OrderPlaced += OnSentOrderAccepted;
        _execution.OrderCancelled += OnOrderCancelled;

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

    public void SendOrder(Order order, bool isFakeOrder = true)
    {
        // this new order's id may or may not be used by external
        // eg. binance uses it
        order.ExternalOrderId = _idGenerator.NewTimeBasedId;
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
        _execution.OrderCancelled -= OnOrderCancelled;
    }

    public Order CreateManualOrder(Security security,
                                   int accountId,
                                   decimal price,
                                   decimal quantity,
                                   Side side,
                                   OrderType orderType = OrderType.Limit,
                                   OrderTimeInForceType timeInForce = OrderTimeInForceType.GoodTillCancel)
    {
        var id = _idGenerator.NewTimeBasedId;
        var now = DateTime.UtcNow;
        return new Order
        {
            Id = id,
            AccountId = accountId,
            BrokerId = 0, // TODO
            CreateTime = now,
            UpdateTime = now,
            ExchangeId = ExchangeIds.GetId(security.Exchange),
            ExternalOrderId = id,
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
}
