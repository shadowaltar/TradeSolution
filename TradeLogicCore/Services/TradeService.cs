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
    private readonly Context _context;
    private readonly ISecurityService _securityService;
    private readonly IOrderService _orderService;
    private readonly Persistence _persistence;
    private readonly IdGenerator _orderIdGenerator;
    private readonly Dictionary<long, Trade> _trades = new();
    private readonly Dictionary<long, List<Trade>> _tradesByOrderId = new();
    private readonly Dictionary<string, Security> _assets = new();

    public event Action<Trade>? NextTrade;

    public event Action<Trade[]>? NextTrades;

    public TradeService(IExternalExecutionManagement execution,
                        Context context,
                        ISecurityService securityService,
                        IOrderService orderService,
                        Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _securityService = securityService;
        _orderService = orderService;
        _persistence = persistence;
        _orderIdGenerator = IdGenerators.Get<Order>();

        _execution.TradeReceived += OnTradeReceived;
        _execution.TradesReceived += OnTradesReceived;
    }

    public void Initialize()
    {
        lock (_assets)
        {
            _assets.Clear();
            var assets = _securityService.GetAssets(_context.Exchange);
            foreach (var item in assets)
            {
                _assets[item.Code] = item;
            }
        }
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
            trade.FeeAssetId = _assets.ThreadSafeTryGet(trade.FeeAssetCode ?? "", out var asset) ? asset.Id : -1;
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

    public async Task CloseAllPositions(Security security)
    {
        var now = DateTime.UtcNow;
        var previousDay = now.AddDays(-1);

        var trades = await GetTrades(security, previousDay, now, true);
        // combine trades of the same side, and send one single inverted-side order for each group
        var bySides = trades.GroupBy(t => t.Side);
        foreach (var tradeGroup in bySides)
        {
            var quantity = tradeGroup.Sum(t => t.Quantity);
            var side = tradeGroup.Key == Side.Buy ? Side.Sell : Side.Buy;

            var order = new Order
            {
                Id = _orderIdGenerator.NewTimeBasedId,
                SecurityCode = security.Code,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
                Quantity = quantity,
                Side = side,
                Type = OrderType.Market,
                Status = OrderStatus.Submitting,
            };
            _orderService.SendOrder(order);
        }
    }

    private void Persist(Trade trade) => _persistence.Enqueue(new PersistenceTask<Trade>(trade) { ActionType = DatabaseActionType.Create });
}
