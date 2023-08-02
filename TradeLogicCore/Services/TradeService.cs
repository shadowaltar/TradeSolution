using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;

namespace TradeLogicCore.Services;
public class TradeService : ITradeService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly IOrderService _orderService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Trade> _trades = new();
    private readonly Dictionary<long, long> _tradeToOrderIds = new();
    private readonly object _lock = new();

    public IReadOnlyDictionary<long, long> TradeToOrderIds => _tradeToOrderIds;

    public event Action<Trade>? NextTrade;

    public event Action<List<Trade>>? NextTrades;

    public TradeService(IExternalExecutionManagement execution,
        IOrderService orderService,
        Persistence persistence)
    {
        _execution = execution;
        _orderService = orderService;
        _persistence = persistence;

        _execution.TradeReceived += OnTradeReceived;
        _execution.TradesReceived += OnTradesReceived;
    }

    private void OnTradeReceived(Trade trade)
    {
        InternalOnNextTrade(trade);

        NextTrade?.Invoke(trade);

        Persist(trade);
    }

    private void OnTradesReceived(List<Trade> trades)
    {
        foreach (var trade in trades)
        {
            InternalOnNextTrade(trade);
        }

        NextTrades?.Invoke(trades);

        foreach (var trade in trades)
        {
            Persist(trade);
        }
    }

    private void InternalOnNextTrade(Trade trade)
    {
        // When a trade is received from external system execution engine
        // parser logic, it might only have external order id info.
        // Need to associate the order and the trade here.
        if (trade.ExternalTradeId == Trade.DefaultId)
        {
            _log.Error("The external system's trade id of a trade must exist.");
            return;
        }
        if (trade.ExternalOrderId == Trade.DefaultId)
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


    }

    private void Persist(Trade trade)
    {
        var tradeTask = new PersistenceTask<Trade>()
        {
            Entry = trade,
            DatabaseName = DatabaseNames.ExecutionData
        };
        _persistence.Enqueue(tradeTask);
    }

    public void Dispose()
    {
        _execution.TradeReceived -= OnTradeReceived;
    }

    public async Task<List<Trade>?> GetMarketTrades(Security security)
    {
        var state = await _execution.GetMarketTrades(security);
        return state.Content;
    }

    public Task<List<Trade>?> GetTrades(Security security)
    {
        throw new NotImplementedException();
    }
}
