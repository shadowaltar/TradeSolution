using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Providers;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Services;
public class PortfolioService : IPortfolioService, IDisposable
{
    private static readonly ILog _log = Logger.New();
    private readonly IdGenerator _orderIdGenerator;
    private readonly IdGenerator _positionIdGenerator;
    private readonly IdGenerator _assetIdGenerator;
    private readonly Context _context;
    private readonly IExternalExecutionManagement _execution;
    private readonly IStorage _storage;
    private readonly IOrderService _orderService;
    private readonly ITradeService _tradeService;
    private readonly ISecurityDefinitionProvider _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, long> _orderToPositionIds = new();
    private readonly Dictionary<long, Position> _closedPositions = new();
    private readonly Dictionary<int, Position> _openPositionsBySecurityId = new();
    private readonly object _lock = new();

    public Portfolio InitialPortfolio { get; private set; }

    public Portfolio Portfolio { get; private set; }

    public event Action<Position>? PositionCreated;
    public event Action<Position>? PositionUpdated;
    public event Action<Position>? PositionClosed;

    public PortfolioService(IExternalExecutionManagement execution,
                            Context context,
                            IOrderService orderService,
                            ITradeService tradeService,
                            ISecurityService securityService,
                            Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _storage = context.Storage;
        _orderService = orderService;
        _tradeService = tradeService;
        _securityService = securityService;
        _persistence = persistence;

        _tradeService.NextTrade += OnNewTrade;
        _execution.BalancesChanged += OnBalancesChanged;

        _orderIdGenerator = IdGenerators.Get<Order>();
        _positionIdGenerator = IdGenerators.Get<Position>();
        _assetIdGenerator = IdGenerators.Get<Asset>();
    }

    private void OnBalancesChanged(List<Asset> assets)
    {
        var account = _context.Account ?? throw Exceptions.MustLogin();
        foreach (var asset in assets)
        {
            var security = _securityService.GetSecurity(asset.SecurityCode);
            if (security == null || !security.IsAsset)
            {
                _log.Error("Received an asset update with unknown asset id, asset code is: " + asset.SecurityCode);
                continue;
            }

            asset.AccountId = account.Id;
            asset.Security = security;
            asset.SecurityId = security.Id;
            asset.SecurityCode = security.Code;
            asset.UpdateTime = DateTime.UtcNow;
        }
        _persistence.Enqueue(assets, isUpsert: true);
    }

    private void OnNewTrade(Trade trade)
    {
        var orderId = trade.OrderId;
        long positionId;
        lock (_lock)
        {
            if (!_orderToPositionIds.TryGetValue(orderId, out positionId))
                return;
        }

        var position = Portfolio.Positions.ThreadSafeGet(positionId, _lock);
        // either create a new position, or merge the trade into it
        if (position != null)
        {
            Apply(position, trade);

            if (position.IsClosed)
            {
                Portfolio.Positions.ThreadSafeRemove(positionId, _lock);
                _openPositionsBySecurityId.ThreadSafeRemove(position.SecurityId, _lock);
                _closedPositions.ThreadSafeSet(positionId, position, _lock);
                PositionClosed?.Invoke(position);
            }
            else
            {
                PositionUpdated?.Invoke(position);
            }
        }
        else
        {
            // must be the order to open a position
            var order = _orderService.GetOrder(orderId);
            if (order == null)
            {
                // a trade without an order which should not happen
                _log.Error("A trade comes without an associated order!");
                return;
            }
            position = Create(trade);

            PositionCreated?.Invoke(position);

            _openPositionsBySecurityId.ThreadSafeSet(position.SecurityId, position, _lock);
            _persistence.Enqueue(position);
        }
    }

    public List<Position> GetOpenPositions()
    {
        var results = Portfolio.Positions.ThreadSafeValues(_lock);
        results.Sort((r1, r2) => r1.CreateTime.CompareTo(r2.CreateTime));
        return results;
    }

    public List<Position> GetClosedPositions()
    {
        return _closedPositions.ThreadSafeValues(_lock);
    }

    public List<Asset> GetCurrentBalances()
    {
        throw new NotImplementedException();
    }

    public List<Asset> GetExternalBalances(string externalName)
    {
        throw new NotImplementedException();
    }

    public Position? GetPosition(int securityId)
    {
        return Portfolio.Positions!.GetOrDefault(securityId);
    }

    public Asset GetAsset(int assetId)
    {
        return Portfolio.Assets!.GetOrDefault(assetId) ?? throw Exceptions.MissingAssetPosition(assetId.ToString());
    }

    public Asset GetPositionRelatedQuoteBalance(int securityId)
    {
        var security = _securityService.GetSecurity(securityId);
        var currencyAsset = security.EnsureCurrencyAsset();
        return GetAsset(currencyAsset.Id) ?? throw Exceptions.MissingAssetPosition(currencyAsset.Code);
    }

    public List<Position> GetPositions()
    {
        return Portfolio.Positions.Values.OrderBy(p => p.SecurityCode).ToList();
    }

    public decimal GetRealizedPnl(Security security)
    {
        return Portfolio.Positions!.GetOrDefault(security.Id, null)?.RealizedPnl ?? 0;
    }

    public ProfitLoss GetUnrealizedPnl(Security security)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> Initialize()
    {
        var account = _context.Account ?? throw Exceptions.MustLogin();
        var state = await _execution.Subscribe();

        if (state.ResultCode == ResultCode.SubscriptionFailed)
        {
            return false;
        }

        var positions = await _context.Storage.ReadPositions(_context.Account);
        foreach (var position in positions)
        {
            position.Security = _securityService.GetSecurity(position.SecurityId);
        }

        // closedPositions need not be initialized
        // currentPosition by SecurityId should be initialized;
        // must be two different instances
        InitialPortfolio = new Portfolio(_context.AccountId, positions);
        Portfolio = new Portfolio(_context.AccountId, positions);
        if (!positions.IsNullOrEmpty())
        {
            foreach (var position in positions)
            {
                if (position.IsClosed)
                    continue;
                if (position.Security.IsAsset)
                    throw Exceptions.InvalidSecurity(position.SecurityCode, "Expect a non-asset security here.");

                InitialPortfolio.Positions[position.Id] = position;
                Portfolio.Positions[position.Id] = position;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a new position entry by a trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public Position Create(Trade trade)
    {
        var position = new Position
        {
            Id = _positionIdGenerator.NewTimeBasedId,
            AccountId = _context.AccountId,

            Security = trade.Security,
            SecurityId = trade.SecurityId,
            SecurityCode = trade.SecurityCode,

            CreateTime = trade.Time,
            UpdateTime = trade.Time,
            CloseTime = DateTime.MaxValue,

            Quantity = trade.Quantity,
            Price = trade.Price,
            LockedQuantity = 0,
            Notional = trade.Quantity * trade.Price,
            StartNotional = trade.Quantity * trade.Price,
            RealizedPnl = 0,

            StartOrderId = trade.OrderId,
            StartTradeId = trade.Id,
            EndOrderId = 0,
            EndTradeId = 0,
        };

        return position;
    }

    public void Apply(Position position, Trade trade)
    {
        if (trade.SecurityId != position.SecurityId
            || trade.AccountId != position.AccountId)
            throw Exceptions.InvalidTradePositionCombination("The trade does not belong to the position.");

        var sign = trade.Side == Side.Sell ? -1 : trade.Side == Side.Buy ? 1 : 0;

        var notional = position.Notional + sign * trade.Price * trade.Quantity;
        var quantity = position.Quantity + sign * trade.Quantity;

        position.Notional = notional;
        position.Quantity = quantity;
        position.Price = notional / quantity;
        position.AccumulatedFee += trade.Fee;

        if (quantity == 0)
        {
            position.RealizedPnl = position.Notional - position.StartNotional;
            position.EndOrderId = trade.OrderId;
            position.CloseTime = trade.Time;
        }

        position.UpdateTime = trade.Time;
    }

    public Order CreateCloseOrder(Asset position, Security? securityOverride = null)
    {
        if (position.SecurityCode.IsBlank()) throw Exceptions.InvalidPosition(position.Id, "missing security code");
        if (_context.Account == null) throw Exceptions.MustLogin();

        var secId = securityOverride?.Id ?? position.SecurityId;
        var security = _securityService.GetSecurity(secId);

        return new Order
        {
            Id = _orderIdGenerator.NewTimeBasedId,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
            Quantity = position.Quantity,
            Side = position.Quantity > 0 ? Side.Sell : Side.Buy,
            Type = OrderType.Market,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Status = OrderStatus.Sending,

            AccountId = _context.AccountId,
            ExternalOrderId = _orderIdGenerator.NewNegativeTimeBasedId, // this is a temp one, should be updated later
            FilledQuantity = 0,
            ExternalCreateTime = DateTime.MinValue,
            ExternalUpdateTime = DateTime.MinValue,
            ParentOrderId = 0,
            Price = 0,
            StopPrice = 0,

            Security = security,
            SecurityId = secId,
            SecurityCode = security.Code,
        };
    }

    public async Task CloseAllPositions()
    {
        // if it is non-fx, create orders to expunge the long/short positions
        var positions = Portfolio.Positions.ThreadSafeValues(_lock);
        foreach (var position in positions)
        {
            var order = CreateCloseOrder(position);
            await _orderService.SendOrder(order);
        }

        // if it is fx+crypto, sell all non-basic assets if previously traded
        var assets = Portfolio.Assets.ThreadSafeValues(_lock);
        foreach (var asset in assets)
        {
            if (_context.PreferredAssetCodes.Contains(asset.SecurityCode))
                continue;

            // cannot close an asset directly, need to pair it up with a quote asset
            // eg. to close BTC position, with known preferred assets USDT and TUSD,
            // will try to use BTCUSDT to close position, then BTCTUSD if BTCUSDT is not available
            Security? security = null;
            var codes = _context.PreferredAssetCodes.Select(c => asset.SecurityCode + c).ToList();
            foreach (var code in codes)
            {
                security = _securityService.GetSecurity(code);
                if (security != null) break;
            }

            if (security != null)
            {
                var order = CreateCloseOrder(asset, security);
                await _orderService.SendOrder(order);
            }

            // TEMP TODO
            break;
        }
    }

    ///// <summary>
    ///// Merges a trade entry into the corresponding position.
    ///// </summary>
    ///// <param name="position"></param>
    ///// <param name="trade"></param>
    //public void Merge(Position position, Trade trade)
    //{
    //    if (position.SecurityId != trade.SecurityId)
    //    {
    //        _log.Error($"Must merge a trade into a position with the same security Id (t:{trade.SecurityId} vs p:{position.SecurityId}).");
    //        return;
    //    }

    //    var newQuantity = position.Quantity + (((int)trade.Side) * trade.Quantity);
    //    var oldValue = position.Price * position.Quantity;
    //    var tradeValue = trade.Price * trade.Quantity;
    //    var newValue = oldValue + (((int)trade.Side) * tradeValue);

    //    var newPrice = position.Price;
    //    var newPnl = 0m;
    //    if (Math.Sign(position.Quantity) != (int)trade.Side)
    //    {
    //        // decreasing the size of position, so the average price should not change
    //        newPnl = tradeValue - (position.Price * trade.Quantity);
    //    }
    //    else
    //    {
    //        // increasing the size of position, so no new Realized Pnl
    //        newPrice = newValue / newQuantity;
    //    }

    //    position.UpdateTime = trade.Time;
    //    position.Quantity = newQuantity;
    //    position.Price = newPrice;
    //    position.RealizedPnl += newPnl;
    //}

    public void Dispose()
    {
        _tradeService.NextTrade -= OnNewTrade;
    }

    public bool Validate(Order order)
    {
        // TODO
        return true;
    }

    public void SpendAsset(int securityId, decimal quantity)
    {
        var asset = GetPositionRelatedQuoteBalance(securityId);
        asset.Quantity -= quantity;
    }

    public decimal Realize(int securityId, decimal realizedPnl)
    {
        if (Portfolio.Positions.TryGetValue(securityId, out var position))
        {
            position.RealizedPnl += realizedPnl;

            var assetPosition = GetPositionRelatedQuoteBalance(securityId);
            assetPosition.Quantity += realizedPnl;

            return position.RealizedPnl;
        }
        return decimal.MinValue;
    }

    public async Task<Asset> Deposit(int assetId, decimal quantity)
    {
        // TODO external logic!
        var asset = GetAsset(assetId);
        if (asset != null)
        {
            asset.Quantity += quantity;
            // TODO external logic
            var assets = await _storage.ReadAssets(Portfolio.AccountId);
            asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            return asset;
        }
        throw Exceptions.MissingAsset(assetId);
    }

    public async Task<Asset?> Deposit(int accountId, int assetId, decimal quantity)
    {
        // TODO external logic!
        var assets = await _storage.ReadAssets(accountId);
        var asset = assets.FirstOrDefault(b => b.SecurityId == assetId);
        if (asset == null)
        {
            var security = _securityService.GetSecurity(assetId);
            asset = new Asset
            {
                Id = _assetIdGenerator.NewTimeBasedId,
                AccountId = accountId,
                Security = security,
                SecurityId = security.Id,
                SecurityCode = security.Code,
                Quantity = quantity,
                LockedQuantity = 0,
                StrategyLockedQuantity = 0,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
            };
            if (await _storage.InsertOne(asset, false) > 0)
                return asset;
            else
            {
                if (Portfolio.AccountId == accountId)
                    throw Exceptions.MissingAsset(assetId);
                else
                    return null;
            }
        }
        else
        {
            asset.Quantity += quantity;
            if (await _storage.InsertOne(asset, true) > 0)
                return asset;
            else
            {
                if (Portfolio.AccountId == accountId)
                    throw Exceptions.MissingAsset(assetId);
                else
                    return null;
            }
        }
    }

    public async Task<Asset?> Withdraw(int assetId, decimal quantity)
    {
        // TODO external logic!
        var asset = GetAsset(assetId);
        if (asset != null)
        {
            if (asset.Quantity < quantity)
            {
                _log.Error($"Attempt to withdraw quantity more than the free amount. Requested: {quantity}, free amount: {asset.Quantity}.");
                return null;
            }
            asset.Quantity -= quantity;
            _log.Info($"Withdrew {quantity} quantity from current account. Remaining free amount: {asset.Quantity}.");
            var assets = await _storage.ReadAssets(Portfolio.AccountId);
            asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            asset.Quantity -= quantity;
            return asset;
        }
        throw Exceptions.MissingAsset(assetId);
    }

    public Position? Reconcile(List<Trade> trades, Position? position = null)
    {
        if (trades.IsNullOrEmpty())
        {
            return position;
        }

        foreach (var trade in trades)
        {
            if (position == null)
                position = Create(trade);
            else
                Apply(position, trade);
        }
        if (position == null) throw Exceptions.Impossible();

        InitialPortfolio.Positions[position.Id] = position;
        Portfolio.Positions[position.Id] = position;

        _openPositionsBySecurityId.ThreadSafeSet(position!.SecurityId, position, _lock);
        _persistence.Enqueue(position);

        PositionCreated?.Invoke(position!);
        return position;
    }

    private void Persist(Position position)
    {
        if (position?.Security == null) throw Exceptions.InvalidPosition(position?.Id, "Missing position or invalid security.");
        _persistence.Enqueue(position);
    }
}
