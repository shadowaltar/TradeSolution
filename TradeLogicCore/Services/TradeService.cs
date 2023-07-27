using Common;
using TradeCommon.Essentials.Trading;
using TradeCommon.Database;
using TradeCommon.Externals;
using log4net;

namespace TradeLogicCore.Services;
public class TradeService : ITradeService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly IOrderService _orderService;
    private readonly MessageBroker<IPersistenceTask> _broker = new();
    private readonly Dictionary<int, Trade> _trades = new();
    private readonly Dictionary<int, int> _tradeToOrderIds = new();
    private readonly object _lock = new();

    public IReadOnlyDictionary<int, int> TradeToOrderIds => _tradeToOrderIds;

    public event Action<Trade>? NewTrade;

    public TradeService(IExternalExecutionManagement execution,
        IOrderService orderService,
        MessageBroker<IPersistenceTask> broker)
    {
        _execution = execution;
        _orderService = orderService;
        _broker = broker;

        _execution.TradeReceived += OnTradeReceived;
    }

    private void OnTradeReceived(Trade trade)
    {
        // When a trade is received from external system execution engine
        // parser logic, it only has external order id info.
        // Need to associate the order and the trade here.
        if (trade.ExternalTradeId == null)
        {
            _log.Error("The external system's trade id of a trade must exist.");
            return;
        }
        if (trade.ExternalOrderId == null)
        {
            _log.Error("The external system's order id of a trade must exist.");
            return;
        }

        var order = _orderService.GetOrderByExternalId(trade.ExternalOrderId);
        if (order == null)
        {
            _log.Error("The associated order of a trade must exist.");
            return;
        }

        lock (_lock)
        {
            trade.OrderId = order.Id;
            _trades[trade.Id] = trade;
            _tradeToOrderIds[trade.Id] = trade.OrderId;
        }
        NewTrade?.Invoke(trade);

        Persist(trade);
    }

    private void Persist(Trade trade)
    {
        var tradeTask = new PersistenceTask<Trade>()
        {
            Entry = trade,
            DatabaseName = DatabaseNames.ExecutionData
        };
        _broker.Enqueue(tradeTask);
    }

    public void Dispose()
    {
        _execution.TradeReceived -= OnTradeReceived;
    }
}
