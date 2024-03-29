﻿using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeDataCore.Essentials;
using TradeDataCore.Instruments;
using static TradeCommon.Utils.Delegates;

namespace TradeLogicCore.Services;
public class TradeService : ITradeService
{
    private static readonly ILog _log = Logger.New();

    private readonly IExternalExecutionManagement _execution;
    private readonly Context _context;
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;
    private readonly Persistence _persistence;
    private readonly IdGenerator _tradeIdGen;
    private readonly Dictionary<long, Trade> _trades = new();
    private readonly Dictionary<long, Trade> _tradesByExternalId = new();
    private readonly Dictionary<long, List<Trade>> _tradesByOrderId = new();
    private readonly Dictionary<string, Security> _assetsByCode = new();
    private readonly Dictionary<int, Security> _assetsById = new();

    public event TradeReceivedCallback? TradeProcessed;

    public event TradesReceivedCallback? NextTrades;

    public TradeService(IExternalExecutionManagement execution,
                        Context context,
                        ISecurityService securityService,
                        IOrderService orderService,
                        IPortfolioService portfolioService,
                        Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _storage = context.Storage;
        _securityService = securityService;
        _orderService = orderService;
        _portfolioService = portfolioService;
        _persistence = persistence;
        _tradeIdGen = IdGenerators.Get<Trade>();

        _execution.TradeReceived -= OnTradeReceived;
        _execution.TradeReceived += OnTradeReceived;
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

        _persistence.Insert(trade);
        TradeProcessed?.Invoke(trade);

        _portfolioService.Process(trade);
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

        // order shall be always handled before its trade
        var order = _orderService.GetOrderByExternalId(trade.ExternalOrderId);
        if (order == null)
        {
            _log.Error("The associated order of a trade must exist.");
            return;
        }

        // operational trades should not affect positions
        var isOperational = _orderService.IsOperational(order.Id);

        // resolve fee asset id here
        if (!trade.FeeAssetCode.IsBlank() && trade.FeeAssetId <= 0)
        {
            trade.FeeAssetId = _assetsByCode.ThreadSafeTryGet(trade.FeeAssetCode ?? "", out var asset) ? asset.Id : 0;
        }
        if (trade.FeeAssetCode.IsBlank() && trade.FeeAssetId != 0)
        {
            trade.FeeAssetCode = _assetsById.ThreadSafeTryGet(trade.FeeAssetId, out var asset) ? asset.Code : "";
        }

        _securityService.Fix(trade);

        trade.IsOperational = isOperational;
        trade.SecurityId = order.SecurityId;
        trade.Security = order.Security;
        trade.OrderId = order.Id;
        _trades.ThreadSafeSet(trade.Id, trade);
        _tradesByExternalId.ThreadSafeSet(trade.ExternalTradeId, trade);

        // update the order actual price and filled quantity
        var trades = UpdateTradeToOrderCache(trade);
        lock (_tradesByOrderId)
        {
            // update order info from trade
            order.Price = decimal.Round(trades.WeightedAverage(t => t.Price, t => t.Quantity), order.Security.PricePrecision);
            order.FilledQuantity = trades.Sum(t => t.Quantity);
            _persistence.Insert(order);
        }
    }

    public async Task<List<Trade>> GetMarketTrades(Security security)
    {
        var state = await _execution.GetMarketTrades(security);
        return state.Get<List<Trade>>();
    }

    public async Task<List<Trade>> GetExternalTrades(Security security, DateTime? start = null, DateTime? end = null)
    {
        var state = await _execution.GetTrades(security, start: start, end: end);
        var trades = state.GetAll<Trade>() ?? new();
        Update(trades, security);
        return trades;
    }

    public async Task<List<Trade>> GetStorageTrades(Security security, DateTime? start = null, DateTime? end = null, bool? isOperational = false)
    {
        var s = start ?? DateTime.MinValue;
        var e = end ?? DateTime.MaxValue;
        var trades = await _storage.ReadTrades(security, s, e, isOperational);
        Update(trades, security);
        return trades;
    }

    public List<Trade> GetTrades(Security security, DateTime? start = null, DateTime? end = null)
    {
        var s = start ?? DateTime.MinValue;
        var e = end ?? DateTime.MaxValue;
        var trades = _trades.ThreadSafeValues()
            .Where(t => t.SecurityId == security.Id && t.Time >= s && t.Time <= e).ToList();
        return trades;
    }

    public List<Trade> GetTradesByOrderId(long orderId)
    {
        lock (_tradesByOrderId)
            return _tradesByOrderId.ThreadSafeGet(orderId)?.ToList() ?? new();
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
            trades = await _storage.ReadTradesByOrderId(security, orderId);
        }
        trades ??= new List<Trade>();
        foreach (var trade in trades)
        {
            trade.SecurityCode = security.Code;
        }
        return trades;
    }

    public void Reset()
    {
        _execution.TradeReceived -= OnTradeReceived;

        _trades.ThreadSafeClear();
        _tradesByExternalId.ThreadSafeClear();
        _tradesByOrderId.ThreadSafeClear();
        _assetsByCode.ThreadSafeClear();
        _assetsById.ThreadSafeClear();
    }

    public void Update(ICollection<Trade> trades, Security? security = null)
    {
        foreach (var trade in trades)
        {
            if (trade.Id <= 0 || trade.PositionId <= 0)
            {
                // to avoid case that incoming trades are actually already cached even they don't have id assigned yet
                var sameTrade = _tradesByExternalId.GetOrDefault(trade.ExternalTradeId);
                if (sameTrade != null)
                {
                    trade.Id = sameTrade.Id;
                    trade.PositionId = sameTrade.PositionId;
                }
                else
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

            _securityService.Fix(trade, security);
            _trades.ThreadSafeSet(trade.Id, trade);
            _tradesByExternalId.ThreadSafeSet(trade.ExternalTradeId, trade);
        }

        foreach (var trade in trades)
            UpdateTradeToOrderCache(trade);
    }

    /// <summary>
    /// Update the trade - order cache, while key is order id.
    /// Returns all the trades with the same order id.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    private List<Trade> UpdateTradeToOrderCache(Trade trade)
    {
        lock (_tradesByOrderId)
        {
            var trades = _tradesByOrderId.GetOrCreate(trade.OrderId);
            var existingIndex = trades.FindIndex(t => t.Id == trade.Id);
            if (existingIndex != -1)
                trades[existingIndex] = trade;
            else
                trades.Add(trade);
            return trades;
        }
    }

    public void ClearCachedClosedPositionTrades(Position? position = null)
    {
        if (_trades.IsNullOrEmpty()) return;

        if (position != null && position.IsClosed)
        {
            lock (_trades)
            {
                var security = position.Security;
                var trades = AsyncHelper.RunSync(() => _context.Storage.ReadTradesByPositionId(security, position.Id, OperatorType.Equals));
                var tradeIds = trades.Select(t => t.Id);
                Clear(tradeIds);
            }
        }
        else if (position == null)
        {
            var allTrades = _trades.ThreadSafeValues();
            var start = allTrades.Min(t => t.Time);
            var positions = AsyncHelper.RunSync(() => _context.Services.Portfolio.GetStoragePositions(start, OpenClose.ClosedOnly));
            var groupedTrades = allTrades.GroupBy(t => t.Security);
            foreach (var group in groupedTrades)
            {
                var tradeIds = group.Where(t => positions.Any(p => p.Id == t.PositionId)).Select(t => t.Id);
                Clear(tradeIds);
            }
        }

        void Clear(IEnumerable<long> tradeIds)
        {
            lock (_trades)
            {
                foreach (var id in tradeIds)
                {
                    var trade = _trades.GetOrDefault(id);
                    _trades.ThreadSafeRemove(id);
                    if (trade != null)
                    {
                        _tradesByExternalId.ThreadSafeRemove(trade.ExternalTradeId);
                        _tradesByOrderId.ThreadSafeRemove(trade.OrderId);
                    }
                }
            }
        }
    }
}
