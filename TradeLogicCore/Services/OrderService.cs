using Common;
using TradeCommon.Essentials.Trading;
using TradeCommon.Database;
using TradeCommon.Constants;

namespace TradeLogicCore.Services;

public class OrderService : IOrderService
{
    private readonly MessageBroker<IPersistenceTask> _broker = new();

    public event Action<Order> NewOrder;

    public OrderService(MessageBroker<IPersistenceTask> broker)
    {
        _broker = broker;
    }

    public void ProcessOrder(Order order)
    {
        var orderTask = new PersistenceTask<Order>()
        {
            DatabaseName = DatabaseNames.ExecutionData
        };
        _broker.Enqueue(orderTask);
    }
}
