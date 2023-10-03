using Common;
using log4net;
using Microsoft.IdentityModel.Tokens;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
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
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly IOrderService _orderService;
    private readonly Persistence _persistence;
    private readonly IdGenerator _orderIdGen;
    private readonly IdGenerator _tradeIdGen;
    private readonly Dictionary<long, Trade> _trades = new();
    private readonly Dictionary<long, Trade> _tradesByExternalId = new();
    private readonly Dictionary<long, List<Trade>> _tradesByOrderId = new();
    private readonly Dictionary<string, Security> _assetsByCode = new();
    private readonly Dictionary<int, Security> _assetsById = new();

    public event Action<Trade>? NextTrade;

    public event Action<List<Trade>>? NextTrades;

    public TradeService(IExternalExecutionManagement execution,
                        Context context,
                        ISecurityService securityService,
                        IOrderService orderService,
                        Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _storage = context.Storage;
        _securityService = securityService;
        _orderService = orderService;
        _persistence = persistence;
        _orderIdGen = IdGenerators.Get<Order>();
        _tradeIdGen = IdGenerators.Get<Trade>();

        _execution.TradeReceived -= OnTradeReceived;
        _execution.TradesReceived -= OnTradesReceived;
        _execution.TradeReceived += OnTradeReceived;
        _execution.TradesReceived += OnTradesReceived;
    }

    public void Initialize()
    {
        lock (_assetsByCode)
        {
            _assetsByCode.Clear();
            var assets = _securityService.GetAssets(_context.Exchange);
            foreach (var asset in assets)
            {
                _assetsByCode[asset.Code] = asset;
            }
        }
        lock (_assetsById)
        {
            _assetsById.Clear();
            foreach (var (_, asset) in _assetsByCode)
            {
                _assetsById[asset.Id] = asset;
            }
        }
    }

    private void OnTradeReceived(Trade trade)
    {
        InternalOnNextTrade(trade);
        UpdateTradeByOrderId(trade);
        UpdatePositionId(trade);
        NextTrade?.Invoke(trade);
        _persistence.Enqueue(trade);
    }

    private void UpdatePositionId(Trade trade)
    {
        var position = _context.Services.Portfolio.GetPositionBySecurityId(trade.SecurityId)
            ?? _context.Services.Portfolio.CreateOrUpdate(trade);
        trade.PositionId = position.Id;
    }

    private void OnTradesReceived(List<Trade> trades)
    {
        foreach (var trade in trades)
        {
            InternalOnNextTrade(trade);
        }
        UpdateTradesByOrderId(trades);
        NextTrades?.Invoke(trades);
        _persistence.Enqueue(trades);
    }

    private void InternalOnNextTrade(Trade trade)
    {
        // When a trade is received from external system execution engine
        // parser logic, it might only have external order id info.
        // Need to associate the order and the trade here.
        if (trade.ExternalTradeId <= 0)
        {
            _log.Error("The external system's trade id of a trade must exist.");
            return;
        }
        if (trade.ExternalOrderId <= 0)
        {
            _log.Error("The external system's order id of a trade must exist.");
            return;
        }

        // order is always handled before trade
        var order = _orderService.GetOrderByExternalId(trade.ExternalOrderId);
        if (order == null)
        {
            _log.Error("The associated order of a trade must exist.");
            return;
        }

        // resolve fee asset id here
        if (!trade.FeeAssetCode.IsBlank() && trade.FeeAssetId <= 0)
        {
            trade.FeeAssetId = _assetsByCode.ThreadSafeTryGet(trade.FeeAssetCode ?? "", out var asset) ? asset.Id : 0;
        }
        if (trade.FeeAssetCode.IsBlank() && trade.FeeAssetId != 0)
        {
            trade.FeeAssetCode = _assetsById.ThreadSafeTryGet(trade.FeeAssetId, out var asset) ? asset.Code : "";
        }
        trade.SecurityId = order.SecurityId;
        trade.Security = order.Security;
        trade.OrderId = order.Id;
        _trades.ThreadSafeSet(trade.Id, trade);
        _tradesByExternalId.ThreadSafeSet(trade.ExternalTradeId, trade);

        if (_tradesByOrderId.ThreadSafeTryGet(order.Id, out var ts))
        {
            ts = _tradesByOrderId.GetOrCreate(order.Id);
            ts.Add(trade);
        }

        // try to find related position id, if not exist, create one immediately
        var position = _context.Services.Portfolio.GetPositionBySecurityId(trade.SecurityId);
        if (position == null)
        {
            // 
        }
    }

    public void Dispose()
    {
        _execution.TradeReceived -= OnTradeReceived;
    }

    public async Task<List<Trade>> GetMarketTrades(Security security)
    {
        var state = await _execution.GetMarketTrades(security);
        return state.Get<List<Trade>>();
    }

    public async Task<List<Trade>> GetTrades(Security security, DateTime? start = null, DateTime? end = null, bool requestExternal = false)
    {
        List<Trade>? trades;
        if (requestExternal)
        {
            var state = await _execution.GetTrades(security, start: start, end: end);
            trades = state.GetAll<Trade>() ?? new();
        }
        else
        {
            var s = start ?? DateTime.MinValue;
            var e = end ?? DateTime.MaxValue;
            trades = await _storage.ReadTrades(security, s, e);
        }
        Update(trades, security);
        return trades ?? new List<Trade>();
    }

    public List<Trade> GetTrades(long orderId)
    {
        lock (_tradesByOrderId)
        {
            return _tradesByOrderId.GetOrCreate(orderId).ToList();
        }
    }

    public async Task<List<Trade>> GetTrades(Security security, long orderId, bool requestExternal = false)
    {
        List<Trade>? trades;
        if (requestExternal)
        {
            var order = _orderService.GetOrder(orderId);
            if (order == null) return new(); // TODO if an order is not cached in order service, this returns null

            var state = await _execution.GetTrades(security, order.ExternalOrderId);
            trades = state.GetAll<Trade>();
        }
        else
        {
            trades = await _storage.ReadTrades(security, orderId);
        }
        trades ??= new List<Trade>();
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
                Id = _orderIdGen.NewTimeBasedId,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
                Quantity = quantity,
                Side = side,
                Type = OrderType.Market,
                Status = OrderStatus.Sending,

                Security = security,
                SecurityId = security.Id,
                SecurityCode = security.Code,
            };
            await _orderService.SendOrder(order);
        }
    }

    public void Update(ICollection<Trade> trades, Security? security = null)
    {
        foreach (var trade in trades)
        {
            if (trade.Id <= 0)
            {
                trade.Id = _tradeIdGen.NewTimeBasedId;
            }
            if (trade.OrderId <= 0)
            {
                var order = _orderService.GetOrderByExternalId(trade.ExternalOrderId);
                if (order == null || trade.ExternalOrderId <= 0)
                {
                    _log.Error("Failed to find corresponding order by trade external-order-id, or the id is invalid.");
                }
                else
                {
                    trade.OrderId = order.Id;
                }
            }

            if (!trade.FeeAssetCode.IsBlank() && trade.FeeAssetId <= 0)
            {
                trade.FeeAssetId = _assetsByCode.ThreadSafeTryGet(trade.FeeAssetCode ?? "", out var asset) ? asset.Id : 0;
            }
            if (trade.FeeAssetCode.IsBlank() && trade.FeeAssetId != 0)
            {
                trade.FeeAssetCode = _assetsById.ThreadSafeTryGet(trade.FeeAssetId, out var asset) ? asset.Code : "";
            }

            if (!_securityService.Fix(trade, security)) continue;

            _trades.ThreadSafeSet(trade.Id, trade);
            _tradesByExternalId.ThreadSafeSet(trade.ExternalTradeId, trade);
        }
        UpdateTradesByOrderId(trades);
    }

    private void UpdateTradesByOrderId(ICollection<Trade> trades)
    {
        lock (_tradesByOrderId)
        {
            foreach (var trade in trades)
            {
                var ts = _tradesByOrderId.GetOrCreate(trade.OrderId);
                var existingIndex = ts.FindIndex(t => t.Id == trade.Id);
                if (existingIndex != -1)
                    ts[existingIndex] = trade;
                else
                    ts.Add(trade);
            }
        }
    }

    private void UpdateTradeByOrderId(Trade trade)
    {
        lock (_tradesByOrderId)
        {
            var ts = _tradesByOrderId.GetOrCreate(trade.OrderId);
            var existingIndex = ts.FindIndex(t => t.Id == trade.Id);
            if (existingIndex != -1)
                ts[existingIndex] = trade;
            else
                ts.Add(trade);
        }
    }

    private void UpdateOrderFilledPrice(long orderId)
    {
        var order = _orderService.GetOrder(orderId);
        if (order == null) throw Exceptions.InvalidTradeServiceState("Expect an order already cached when requesting it from TradeService, orderId " + orderId);

        var trades = _tradesByOrderId.ThreadSafeGet(orderId);
        if (trades == null) throw Exceptions.InvalidTradeServiceState("Must cache before update order filled price, orderId " + orderId);

        var sumProduct = trades.Sum(t => t.Price * t.Quantity);
        var sumQuantity = trades.Sum(t => t.Quantity);
        var weightedPrice = sumProduct / sumQuantity;
        if (order.Price == weightedPrice)
            return;

        _orderService.Persist(order);
    }
}
