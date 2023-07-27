using Common;
using TradeCommon.Essentials.Trading;
using TradeCommon.Database;
using TradeCommon.Constants;

namespace TradeLogicCore.Services;
public class TradeService : ITradeService
{
    private readonly MessageBroker<IPersistenceTask> _broker = new();

    private readonly Dictionary<int, int> _tradeToOrderIds = new();
    private readonly Dictionary<int, Trade> _trades = new();
    private readonly object _lock = new ();

    public IReadOnlyDictionary<int, int> TradeToOrderIds => _tradeToOrderIds;

    public event Action<Trade>? NewTrade;

    public void ProcessTrade(Trade trade)
    {
        lock (_lock)
        {
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
            DatabaseName = DatabaseNames.ExecutionData
        };
        _broker.Enqueue(tradeTask);
    }
}
