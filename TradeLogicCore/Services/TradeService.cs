using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Services;
public class TradeService : ITradeService, IDisposable
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly ISecurityService _securityService;
    private readonly IOrderService _orderService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Trade> _trades = new();
    private readonly Dictionary<long, List<Trade>> _tradesByOrderId = new();
    private readonly Dictionary<string, Security> _assets = new();

    public event Action<Trade>? NextTrade;

    public event Action<Trade[]>? NextTrades;

    public TradeService(IExternalExecutionManagement execution,
                        ISecurityService securityService,
                        IOrderService orderService,
                        Persistence persistence)
    {
        _execution = execution;
        _securityService = securityService;
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

    private void OnTradesReceived(Trade[] trades)
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

        var order = _orderService.GetOpenOrderByExternalId(trade.ExternalOrderId);
        if (order == null)
        {
            _log.Error("The associated order of a trade must exist.");
            return;
        }

        lock (_assets)
        {
            if (_assets.IsNullOrEmpty())
            {
                var assets = _securityService.GetAssets();
                foreach (var a in assets)
                {
                    _assets[a.Code] = a;
                }
            }
        }

        trade.SecurityId = order.SecurityId;
        trade.OrderId = order.Id;
        trade.FeeAssetId = _assets.ThreadSafeTryGet(trade.FeeAssetCode ?? "", out var asset) ? asset.Id : -1;
        _trades.ThreadSafeSet(trade.Id, trade);
    }

    public void Dispose()
    {
        _execution.TradeReceived -= OnTradeReceived;
    }

    public async Task<List<Trade>> GetMarketTrades(Security security)
    {
        var state = await _execution.GetMarketTrades(security);
        return state.ContentAs<List<Trade>>()!;
    }

    public async Task<List<Trade>> GetTrades(Security security, DateTime? start = null, DateTime? end = null, bool requestExternal = false)
    {
        List<Trade> trades;
        if (requestExternal)
        {
            var state = await _execution.GetTrades(security, start: start, end: end);
            trades = state.ContentAs<List<Trade>>()!;
        }
        else
        {
            var s = start ?? DateTime.MinValue;
            var e = end ?? DateTime.MaxValue;
            trades = await Storage.ReadTrades(security, s, e);
        }
        foreach (var trade in trades)
        {
            trade.SecurityCode = security.Code;
        }
        return trades;
    }

    public async Task<List<Trade>> GetTrades(Security security, long orderId, bool requestExternal = false)
    {
        List<Trade>? trades;
        if (requestExternal)
        {
            var order = _orderService.GetOrder(orderId);
            if (order == null) return new(); // TODO if an order is not cached in order service, this returns null

            var state = await _execution.GetTrades(security, order.ExternalOrderId);
            trades = state.ContentAs<List<Trade>>() ?? new();
            _tradesByOrderId.ThreadSafeSet(orderId, trades); // replace anything cached directly
        }
        else
        {
            trades = await Storage.ReadTrades(security, orderId);
            _tradesByOrderId.ThreadSafeSet(orderId, trades);
        }
        foreach (var trade in trades)
        {
            trade.SecurityCode = security.Code;
        }
        return trades;
    }

    private void Persist(Trade trade) => _persistence.Enqueue(new PersistenceTask<Trade>(trade) { ActionType = DatabaseActionType.Create });
}
