using Common;
using TradeCommon.Essentials.Trading;
using TradeCommon.Database;
using TradeCommon.Externals;
using log4net;

namespace TradeLogicCore.Services;

public class OrderService : IOrderService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly MessageBroker<IPersistenceTask> _broker = new();
    private readonly Dictionary<int, Order> _orders = new();
    private readonly Dictionary<int, Order> _cancelledOrders = new();
    private readonly object _lock = new();

    public event Action<Order> OrderCreated;
    public event Action<Order> OrderCancelled;

    public OrderService(IExternalExecutionManagement execution,
        MessageBroker<IPersistenceTask> broker)
    {
        _execution = execution;
        _broker = broker;

        _execution.OrderPlaced += OnOrderPlaced;
        _execution.OrderCanceled += OnOrderCancelled;
    }

    public Order? GetOrder(int orderId)
    {
        return _orders.TryGetValue(orderId, out var order) ? order : null;
    }

    public void PlaceOrder(Order order)
    {
        _execution.PlaceOrder(order);
    }

    public void CancelOrder(int orderId)
    {
        var order = GetOrder(orderId);
        if (order != null)
        {
            _execution.CancelOrder(order);
        }
    }

    private void OnOrderPlaced(bool isSuccessful, Order order)
    {
        lock (_lock)
            _orders[order.Id] = order;

        OrderCreated?.Invoke(order);
        Persist(order);
    }

    private void OnOrderCancelled(bool isSuccessful, Order order)
    {
        lock (_lock)
        {
            _orders.Remove(order.Id);
            _cancelledOrders[order.Id] = order;
        }
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
        _broker.Enqueue(orderTask);
    }

    public void Dispose()
    {
        _execution.OrderPlaced -= OnOrderPlaced;
        _execution.OrderCanceled -= OnOrderCancelled;
    }
}
