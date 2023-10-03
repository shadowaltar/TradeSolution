using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
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
    private readonly Dictionary<long, Position> _closedPositions = new();
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

        _tradeService.NextTrade -= OnNewTrade;
        _tradeService.NextTrade += OnNewTrade;
        _tradeService.NextTrades -= OnNewTrades;
        _tradeService.NextTrades += OnNewTrades;
        _execution.BalancesChanged -= OnBalancesChanged;
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

    private void OnNewTrades(List<Trade> trades)
    {
        foreach (var trade in trades)
        {
            OnNewTrade(trade);
        }
    }

    private void OnNewTrade(Trade trade)
    {
        // one security to one position
        var securityId = trade.SecurityId;
        var position = Portfolio.GetPositionBySecurityId(securityId);
        // either create a new position, or merge the trade into it
        if (position != null)
        {
            Apply(position, trade);

            if (position.IsClosed)
            {
                Portfolio.Positions.ThreadSafeRemove(position.Id, _lock);
                _closedPositions.ThreadSafeSet(position.Id, position, _lock);
                PositionClosed?.Invoke(position);
            }
            else
            {
                PositionUpdated?.Invoke(position);
            }
        }
        else
        {
            var orderId = trade.OrderId;
            // must be the order to open a position
            var order = _orderService.GetOrder(orderId);
            if (order == null)
            {
                // a trade without an order which should not happen
                _log.Error("A trade comes without an associated order!");
                return;
            }
            position = Position.Create(trade);
            PositionCreated?.Invoke(position);
        }
        // update existing, or save a new position to db
        _persistence.Enqueue(position);
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

    public Position? GetPosition(long id)
    {
        return Portfolio.Positions!.GetOrDefault(id);
    }

    public Position? GetPositionBySecurityId(int securityId)
    {
        return Portfolio.Positions.Values.FirstOrDefault(p => p.SecurityId == securityId);
    }

    public Asset? GetAsset(long id)
    {
        return Portfolio.Assets.GetOrDefault(id);
    }

    public Asset? GetAssetBySecurityId(int securityId)
    {
        return Portfolio.Assets.Values.FirstOrDefault(a => a.SecurityId == securityId);
    }


    public async Task<List<Asset>> GetAssets(Account account, bool requestExternal = false)
    {
        // TODO only supports single account process
        if (_context.Account != account) throw Exceptions.InvalidAccount();
        List<Asset> assets;
        if (requestExternal)
        {
            var state = await _execution.GetAssetPositions(account.ExternalAccount);
            assets = state.Get<List<Asset>>()!;
        }
        else
        {
            assets = await _storage.ReadAssets(account.Id);
        }
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset)) continue;
        }
        return assets.Where(a => !a.IsSecurityInvalid()).ToList();
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
            if (!_securityService.Fix(position)) continue;
        }

        var assets = await _context.Storage.ReadAssets(_context.AccountId);
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset)) continue;
        }

        // closedPositions need not be initialized
        // currentPosition by SecurityId should be initialized;
        // must be two different instances
        InitialPortfolio = new Portfolio(_context.AccountId, positions, assets);
        Portfolio = new Portfolio(_context.AccountId, positions, assets);
        return true;
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
        position.Price = quantity == 0 ? 0 : notional / quantity;
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

        return new Order
        {
            Id = _orderIdGenerator.NewTimeBasedId,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
            Quantity = Math.Abs(position.Quantity),
            Side = position.Quantity > 0 ? Side.Sell : Side.Buy,
            Type = OrderType.Market,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Status = OrderStatus.Sending,

            AccountId = _context.AccountId,
            ExternalOrderId = 0, // this is a temp one, should be updated later
            FilledQuantity = 0,
            ExternalCreateTime = DateTime.MinValue,
            ExternalUpdateTime = DateTime.MinValue,
            ParentOrderId = 0,
            Price = 0,
            StopPrice = 0,

            Security = position.Security,
            SecurityId = position.SecurityId,
            SecurityCode = position.SecurityCode,
        };
    }

    public async Task CloseAllPositions()
    {
        // if it is non-fx, create orders to expunge the long/short positions
        var positions = Portfolio.Positions.ThreadSafeValues(_lock);
        foreach (var position in positions)
        {
            if (position.IsClosed) continue;

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

    public void Update(List<Position> positions, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.Positions.Clear();
            Portfolio.Positions.Clear();
        }

        foreach (var position in positions)
        {
            if (!_securityService.Fix(position))
            {
                continue;
            }
            if (position.IsClosed)
            {
                _closedPositions[position.Id] = position;
            }
            if (isInitializing)
            {
                InitialPortfolio.Positions[position.Id] = position;
            }
            Portfolio.Positions[position.Id] = position;
        }
    }

    public void Update(List<Asset> assets, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.Assets.Clear();
            Portfolio.Assets.Clear();
        }
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset))
            {
                continue;
            }
            asset.AccountId = _context.AccountId;
            if (isInitializing)
            {
                InitialPortfolio.Assets[asset.Id] = asset;
            }
            Portfolio.Assets[asset.Id] = asset;
        }
    }

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

    public Position CreateOrUpdate(Trade trade, Position? existing = null)
    {
        if (existing == null)
            existing = Position.Create(trade);
        else
            Apply(existing, trade);
        if (existing == null) throw Exceptions.Impossible();

        if (!existing.IsClosed)
        {
            InitialPortfolio.Positions[existing.Id] = existing;
            Portfolio.Positions[existing.Id] = existing;
        }
        else
        {
            existing.EndOrderId = trade.OrderId;
            existing.EndTradeId = trade.Id;
            existing.CloseTime = trade.Time;
        }
        _persistence.Enqueue(existing);
        PositionCreated?.Invoke(existing!);
        return existing;
    }

    public Position? CreateOrUpdate(List<Trade> trades, Position? existing = null)
    {
        if (trades.IsNullOrEmpty())
        {
            return existing;
        }

        var groups = trades.GroupBy(t => t.SecurityId);

        foreach (var groupOfTrades in groups)
        {
            if (existing != null && existing.SecurityId == groupOfTrades.Key)
            {

            }
        }
        foreach (var trade in trades)
        {
            if (existing == null)
                existing = Position.Create(trade);
            else
                Apply(existing, trade);
        }
        if (existing == null) throw Exceptions.Impossible();

        if (!existing.IsClosed)
        {
            InitialPortfolio.Positions[existing.Id] = existing;
            Portfolio.Positions[existing.Id] = existing;
        }
        else
        {
            var lastTrade = trades.Last();
            existing.EndOrderId = lastTrade.OrderId;
            existing.EndTradeId = lastTrade.Id;
            existing.CloseTime = lastTrade.Time;
        }
        _persistence.Enqueue(existing);

        PositionCreated?.Invoke(existing!);
        return existing;
    }
}
