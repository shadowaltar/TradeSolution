using Autofac.Core;
using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Providers;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeDataCore.Instruments;
using TradeLogicCore.Utils;

namespace TradeLogicCore.Services;
public class PortfolioService : IPortfolioService
{
    private static readonly ILog _log = Logger.New();
    private readonly IdGenerator _orderIdGenerator;
    private readonly IdGenerator _assetIdGenerator;
    private readonly Context _context;
    private readonly IExternalExecutionManagement _execution;
    private readonly IStorage _storage;
    private readonly IOrderService _orderService;
    private readonly ISecurityDefinitionProvider _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<int, decimal> _residualByAssetSecurityId = [];

    public event Action<Asset, Trade>? AssetProcessed;
    //public event Action<Asset>? AssetPositionCreated;
    //public event Action<Asset>? AssetPositionUpdated;
    //public event Action<List<Asset>>? AssetPositionsUpdated;
    //public event Action<Asset>? AssetClosed;

    public Portfolio InitialPortfolio { get; private set; }

    public Portfolio Portfolio { get; private set; }

    public bool HasAssetPosition => Portfolio.HasAssetPosition;

    public PortfolioService(IExternalExecutionManagement execution,
                            Context context,
                            IOrderService orderService,
                            ISecurityService securityService,
                            Persistence persistence)
    {
        _execution = execution;
        _context = context;
        _storage = context.Storage;
        _orderService = orderService;
        _securityService = securityService;
        _persistence = persistence;
        _execution.AssetsChanged -= OnAssetsChanged;
        _execution.AssetsChanged += OnAssetsChanged;

        _orderIdGenerator = IdGenerators.Get<Order>();
        _assetIdGenerator = IdGenerators.Get<Asset>();
    }

    public Asset? GetPositionBySecurityId(int securityId)
    {
        return Portfolio.GetAssetPositionBySecurityId(securityId);
    }

    public Asset? GetCash(int securityId)
    {
        return Portfolio.GetCashAssetBySecurityId(securityId);
    }

    public List<Asset> GetAllAssets()
    {
        return Portfolio.GetAll();
    }

    public Asset? GetCashBySecurityId(int securityId)
    {
        return Portfolio.GetCashAssetBySecurityId(securityId);
    }

    //public async Task<List<Position>> GetStoragePositions(DateTime? start = null, OpenClose isOpenOrClose = OpenClose.All)
    //{
    //    start ??= DateTime.MinValue;
    //    var positions = await _context.Storage.ReadPositions(start.Value, isOpenOrClose);
    //    _securityService.Fix(positions);
    //    return positions;
    //}

    public async Task<List<Asset>> GetExternalAssets()
    {
        var state = await _execution.GetAssetPositions(_context.Account.ExternalAccount);
        if (state.ResultCode == ResultCode.GetAccountFailed)
        {
            _log.Error("Failed to get assets.");
            return [];
        }
        var assets = state.Get<List<Asset>>();
        if (assets == null)
        {
            throw Exceptions.Invalid<Asset>("Failed to retrieve assets from external!");
        }
        // at least for binance, some assets are not traded and no security definition is found from binance itself.
        var codesWithoutSecurity = new List<string>();
        foreach (var asset in assets)
        {
            var sec = _securityService.GetSecurity(asset.SecurityCode);
            if (sec == null)
                codesWithoutSecurity.Add(asset.SecurityCode);
            else
                _securityService.Fix(asset, sec);
        }
        if (!codesWithoutSecurity.IsNullOrEmpty())
        {
            _log.Warn("Codes of externally-retrieved assets without security definitions: " + string.Join(", ", codesWithoutSecurity));
        }
        return assets.Where(a => !a.IsSecurityInvalid()).ToList();
    }

    public async Task<List<Asset>> GetStorageAssets()
    {
        var assets = await _storage.ReadAssets();
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);
        }
        return assets.Where(a => !a.IsSecurityInvalid()).ToList();
    }

    public async Task<List<AssetState>> GetAssetStates(Security security, DateTime start)
    {
        return await _storage.ReadAssetStates(security, start);
    }

    public async Task Unsubscribe()
    {
    }

    public async Task<bool> Initialize()
    {
        if (_context.Account == null) throw Exceptions.MustLogin();
        if (!Firewall.CanCall)
        {
            Portfolio = new Portfolio(_context.AccountId);
            InitialPortfolio = new Portfolio(_context.AccountId);
            return true;
        }

        var state = await _execution.Subscribe();

        if (state.ResultCode == ResultCode.SubscriptionFailed)
        {
            return false;
        }

        //var positions = await _context.Services.Portfolio.GetStoragePositions(DateTime.MinValue, OpenClose.OpenOnly);
        //_securityService.Fix(positions);

        var assets = await _context.Storage.ReadAssets();
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);
        }

        // closedPositions need not be initialized
        // currentPosition by SecurityId should be initialized;
        // must be two different instances
        Portfolio = new Portfolio(_context.AccountId, assets);

        InitialPortfolio = new Portfolio(_context.AccountId, MiscExtensions.Clone(assets));
        return true;
    }

    private Order CreateCloseOrder(decimal quantity, Security security, string comment)
    {
        return _context.Account == null
            ? throw Exceptions.MustLogin()
            : new Order
            {
                Id = _orderIdGenerator.NewTimeBasedId,
                Action = OrderActionType.CleanUpLive,

                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
                Quantity = Math.Abs(quantity),
                Side = quantity > 0 ? Side.Sell : Side.Buy,
                Type = OrderType.Market,
                TimeInForce = TimeInForceType.GoodTillCancel,
                Status = OrderStatus.Sending,

                AccountId = _context.AccountId,
                ExternalOrderId = _orderIdGenerator.NewNegativeTimeBasedId,
                FilledQuantity = 0,
                ExternalCreateTime = DateTime.MinValue,
                ExternalUpdateTime = DateTime.MinValue,
                ParentOrderId = 0,
                Price = 0,
                LimitPrice = 0, // MARKET order
                TriggerPrice = 0, // it was not triggered by a price change
                StopPrice = 0,
                Security = security,
                SecurityId = security.Id,
                SecurityCode = security.Code,
                Comment = comment,
            };
    }

    public async Task<bool> CloseAllPositions(string orderComment)
    {
        // if it is non-fx, create orders to expunge the long/short positions
        var assets = Portfolio.GetAssetPositions();
        if (assets.IsNullOrEmpty()) return false;

        if (_context.PreferredQuoteCurrencies.Count == 0)
            throw new InvalidOperationException("Algorithm must be initialized before closing any assets.");

        var count = 0;
        foreach (var asset in assets)
        {
            if (asset.Quantity == 0)
                continue;
            if (asset.Security.IsCash)
                continue;

            if (_context.HasCurrencyWhitelist && !_context.CurrencyWhitelist.Contains(asset.Security))
            {
                _log.Debug($"Whitelist is set and this asset code {asset.SecurityCode} is not in it and will be ignored.");
                continue;
            }

            //if (asset.Security.MinQuantity == 0)
            //{
            //    // some externals need this for calculation like minimal notional amount
            //    var refPrice = await _context.Services.MarketData.GetPrice(asset.Security);
            //    if (refPrice == 0)
            //    {
            //        _log.Error($"Failed to get market price for {asset.SecurityCode}.");
            //    }
            //    else
            //    {
            //        _context.Services.Security.SetSecurityMinQuantity(asset.SecurityCode, refPrice);
            //    }
            //    // not supposed to be zero usually
            //    _log.Info($"Minimum quantity for security {asset.Security.Code} is zero.");
            //}
            //if (asset.Quantity <= asset.Security.MinQuantity)
            //    continue;

            foreach (var quoteCurrency in _context.PreferredQuoteCurrencies)
            {
                var security = _securityService.GetFxSecurity(asset.Security.Code, quoteCurrency.Code);
                if (security == null)
                {
                    _log.Warn($"Cannot close asset {asset.SecurityCode} by quote currency {quoteCurrency.Code}; will try next preferred quote currency.");
                    continue;
                }

                if (security.MinQuantity == 0)
                {
                    // some externals need this for calculation like minimal notional amount
                    var refPrice = await _context.Services.MarketData.GetPrice(security);
                    if (refPrice == 0)
                    {
                        _log.Error($"Failed to get market price for {security.Code}; minimum quantity for security {security.Code} is zero; will try next preferred quote currency.");
                        continue;
                    }
                    else
                    {
                        var min = _context.Services.Security.SetSecurityMinQuantity(security.Code, refPrice);
                        _log.Info($"Retrieved min quantity for security {security.Code}: {min}");
                    }
                }
                if (asset.Quantity <= security.MinQuantity)
                    continue;

                _log.Info($"Selling off all {asset.SecurityCode} asset position.");
                var order = CreateCloseOrder(asset.Quantity, security, orderComment);
                var state = await _orderService.SendOrder(order);
                // need to wait for a while
                if (Threads.WaitUntil(() =>
                {
                    return asset.IsClosed;
                }))
                {
                    _log.Info("Closed asset position: " + asset.SecurityCode);
                    break;
                }
                else
                {
                    _log.Error("Failed to close asset position (timed-out): " + asset.SecurityCode + "; will try next preferred quote currency.");
                }
            }
        }
        _persistence.WaitAll();
        return count > 0;
    }

    //public async Task<bool> CleanUpNonCashAssets(string orderComment)
    //{
    //    // sell any assets which is not cash / fiat
    //    var assets = Portfolio.GetAssets();
    //    if (assets.IsNullOrEmpty()) return false;

    //    var preferredQuoteCurrency = _context.PreferredQuoteCurrencies.FirstOrDefault();
    //    if (preferredQuoteCurrency == null)
    //    {
    //        _log.Error("Failed to get preferred quote currency.");
    //        return false;
    //    }
    //    // fx only logic
    //    List<Asset> assetsToBeCleanedUp = !_context.HasCurrencyWhitelist
    //        ? assets
    //        : assets.Where(a => _context.CurrencyWhitelist.Contains(a.Security)).ToList();
    //    if (assetsToBeCleanedUp.IsNullOrEmpty())
    //        return false;

    //    _log.Info($"Cleaning up {assetsToBeCleanedUp.Count} assets.");
    //    var count = 0;
    //    foreach (var asset in assetsToBeCleanedUp)
    //    {
    //        if (preferredQuoteCurrency.Equals(asset.Security)) continue;
    //        var oldQuoteCurrencyQuantity = Portfolio.GetAssets().FirstOrDefault(a => preferredQuoteCurrency.Equals(a.Security))?.Quantity ?? 0;
    //        var currencyPair = _securityService.GetFxSecurity(asset.SecurityCode, preferredQuoteCurrency.Code);
    //        if (currencyPair == null)
    //        {
    //            _log.Warn($"Unable to clean up asset position for currency pair (base:{asset.SecurityCode}, quote:{preferredQuoteCurrency}).");
    //            continue;
    //        }
    //        if (asset.IsEmpty)
    //            continue;

    //        count++;
    //        var oldQuantity = asset.Quantity;
    //        var order = CreateCloseOrder(oldQuantity, currencyPair, orderComment);
    //        order.Action = OrderActionType.Operational;
    //        var state = await _orderService.SendOrder(order);
    //        if (state.ResultCode == ResultCode.SendOrderOk)
    //        {
    //            var r = Threads.WaitUntil(() =>
    //            {
    //                _persistence.WaitAll();
    //                var recentAssets = AsyncHelper.RunSync(() => GetStorageAssets());
    //                var a = recentAssets.FirstOrDefault(a => a.SecurityCode == asset.SecurityCode);
    //                return a?.IsEmpty ?? true; // meaning that the asset is cleaned up
    //            }, pollingMs: 1000);

    //            if (r)
    //            {
    //                var quoteCurrencyQuantity = Portfolio.GetAssets().FirstOrDefault(a => a.Security.Equals(preferredQuoteCurrency))?.Quantity ?? 0;

    //                _log.Info($"Cleaned up asset {asset.SecurityCode} by selling {currencyPair.Code} with quantity {oldQuantity};" +
    //                    $" quote currency {preferredQuoteCurrency} quantity {oldQuoteCurrencyQuantity}->{quoteCurrencyQuantity}");
    //            }
    //            else
    //            {
    //                _log.Error($"Failed to clean up asset {asset.SecurityCode} (timed-out).");
    //            }
    //        }
    //        else
    //        {
    //            _log.Error($"Failed to clean up asset {asset.SecurityCode} (sell order failed).");
    //        }
    //    }
    //    return count > 0;
    //}

    //public Position? CreateOrApply(Trade trade, Position? position = null)
    //{
    //    if (trade.IsOperational) return position;

    //    if (position == null)
    //    {
    //        var residual = _residualByAssetSecurityId.ThreadSafeGet(trade.Security.FxInfo?.BaseAsset?.Id ?? 0);
    //        position = Position.Create(trade, residual);
    //        _securityService.Fix(position); // must call this for min-notional consideration
    //        if (residual != 0)
    //            _log.Info($"[{trade.SecurityCode}] has residual quantity {residual} which is merged into a new position with Id: {position.Id}");
    //    }
    //    else
    //    {
    //        _securityService.Fix(position); // must call this for min-notional consideration
    //        position.Apply(trade, 0); // residual if exists, only applied during position creation and also placing a close order
    //    }
    //    trade.PositionId = position.Id;
    //    return position;
    //}

    //public void Update(List<Position> positions, bool isInitializing = false)
    //{
    //    if (isInitializing)
    //    {
    //        InitialPortfolio.ClearPositions();
    //        Portfolio.ClearPositions();
    //    }

    //    foreach (var position in positions)
    //    {
    //        _securityService.Fix(position);
    //        if (position.IsClosed)
    //        {
    //            _closedPositions[position.Id] = position;
    //            Portfolio.RemovePosition(position.Id);
    //            if (isInitializing)
    //                InitialPortfolio.RemovePosition(position.Id);
    //        }
    //        else
    //        {
    //            Portfolio.AddOrUpdate(position);
    //        }
    //        if (isInitializing)
    //        {
    //            InitialPortfolio.AddOrUpdate(position with { });
    //        }
    //    }
    //}

    public void Update(List<Asset> assets, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.Clear();
            Portfolio.Clear();
        }
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);
            asset.AccountId = _context.AccountId;
            if (isInitializing)
            {
                InitialPortfolio.AddOrUpdate(asset with { });
            }
            Portfolio.AddOrUpdate(asset);
        }
    }

    public async Task Reset()
    {
        await _execution.Unsubscribe();

        InitialPortfolio.Clear();
        Portfolio.Clear();

        _residualByAssetSecurityId.Clear();
    }

    public bool Validate(Order order)
    {
        // TODO
        return true;
    }

    //public async Task<Asset> Deposit(int assetId, decimal quantity)
    //{
    //    // TODO external logic!
    //    var asset = GetAsset(assetId);
    //    if (asset != null)
    //    {
    //        asset.Quantity += quantity;
    //        // TODO external logic
    //        var assets = await _storage.ReadAssets();
    //        asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
    //        return asset;
    //    }
    //    throw Exceptions.MissingAsset(assetId);
    //}

    public async Task<Asset?> Deposit(int accountId, int assetId, decimal quantity)
    {
        // TODO external logic!
        var assets = await _storage.ReadAssets();
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
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
            };
            return await _storage.InsertOne(asset) > 0
                ? asset
                : Portfolio.AccountId == accountId ? throw Exceptions.MissingAsset(assetId) : null;
        }
        else
        {
            asset.Quantity += quantity;
            return await _storage.UpsertOne(asset) > 0
                ? asset
                : Portfolio.AccountId == accountId ? throw Exceptions.MissingAsset(assetId) : null;
        }
    }

    //public async Task<Asset?> Withdraw(int assetId, decimal quantity)
    //{
    //    // TODO external logic!
    //    var asset = GetAsset(assetId);
    //    if (asset != null)
    //    {
    //        if (asset.Quantity < quantity)
    //        {
    //            _log.Error($"Attempt to withdraw quantity more than the free amount. Requested: {quantity}, free amount: {asset.Quantity}.");
    //            return null;
    //        }
    //        asset.Quantity -= quantity;
    //        _log.Info($"Withdrew {quantity} quantity from current account. Remaining free amount: {asset.Quantity}.");
    //        var assets = await _storage.ReadAssets();
    //        asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
    //        asset.Quantity -= quantity;
    //        return asset;
    //    }
    //    throw Exceptions.MissingAsset(assetId);
    //}

    public async Task Reload(bool clearOnly, bool affectInitialPortfolio)
    {
        Portfolio.Clear();

        if (affectInitialPortfolio)
        {
            InitialPortfolio.Clear();
        }
        if (!clearOnly)
        {
            var assets = await _storage.ReadAssets();
            foreach (var asset in assets)
            {
                _securityService.Fix(asset);
                Portfolio.AddOrUpdate(asset);
                if (affectInitialPortfolio)
                {
                    InitialPortfolio.AddOrUpdate(asset);
                }
            }
        }
    }

    public void Process(List<Trade> trades, bool isSameSecurity)
    {
        if (trades.IsNullOrEmpty())
            return;

        foreach (var trade in trades)
        {
            Process(trade);
        }
    }

    public void Process(Trade trade)
    {
        var asset = GetPositionBySecurityId(trade.SecurityId);
        if (asset == null) throw Exceptions.Impossible();

        _securityService.Fix(asset);
        Portfolio.AddOrUpdate(asset);
        _persistence.Insert(asset);

        // invoke post-events
        //if (asset.)
        //    AssetPositionCreated?.Invoke(asset);
        //else
        //    AssetPositionUpdated?.Invoke(asset);

        //if (asset.IsEmpty)
        //{
        //    AssetClosed?.Invoke(asset);
        //}
        AssetProcessed?.Invoke(asset, trade);
    }

    public Side GetOpenPositionSide(int securityId)
    {
        var asset = GetPositionBySecurityId(securityId);
        return asset == null || asset.IsEmpty ? Side.None : asset.Quantity > 0 ? Side.Buy : Side.Sell;
    }

    public decimal GetAssetPositionResidual(int assetSecurityId)
    {
        return _residualByAssetSecurityId.ThreadSafeGet(assetSecurityId);
    }

    //public void ClearCachedClosedPositions(bool isInitializing = false)
    //{
    //    if (isInitializing)
    //    {
    //        Clear(InitialPortfolio);
    //    }
    //    Clear(Portfolio);

    //    static void Clear(Portfolio portfolio)
    //    {
    //        var openPositions = portfolio.GetPositions().Where(p => !p.IsClosed);
    //        portfolio.ClearPositions();
    //        foreach (var position in openPositions)
    //        {
    //            portfolio.AddOrUpdate(position);
    //        }
    //    }
    //}

    private void OnAssetsChanged(List<Asset> assets)
    {
        var account = _context.Account ?? throw Exceptions.MustLogin();
        var states = new List<AssetState>();
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);

            // basically just use the id from the existing item
            var existingAsset = asset.Security.IsCash ? Portfolio.GetCashAssetBySecurityId(asset.SecurityId) : Portfolio.GetAssetPositionBySecurityId(asset.SecurityId);
            if (existingAsset == null)
            {
                _log.Info($"Adding asset {asset.SecurityCode} with quantity {asset.Quantity}");
            }
            else
            {
                _log.Info($"Updating asset {asset.SecurityCode} with quantity {asset.Quantity}");
            }
            asset.AccountId = existingAsset?.AccountId ?? account.Id;
            asset.UpdateTime = DateTime.UtcNow;
            asset.Id = existingAsset != null ? existingAsset.Id : _assetIdGenerator.NewTimeBasedId;
            states.Add(AssetState.From(asset));

            Portfolio.AddOrUpdate(asset);
        }
        _persistence.Insert(assets, isUpsert: true);
        _persistence.Insert(states, isUpsert: false);
    }

    public List<Asset> GetAssets()
    {
        return Portfolio.GetAssetPositions();
    }

    public List<Asset> GetCashes()
    {
        return Portfolio.GetCashes();
    }

    public Asset? GetAssetBySecurityId(int securityId, bool isInit = false)
    {
        if (isInit)
            return InitialPortfolio.GetAssetPositionBySecurityId(securityId);
        return Portfolio.GetAssetPositionBySecurityId(securityId);
    }

    public Asset? GetRelatedCashPosition(Security security)
    {
        var currencyAsset = security.SafeGetQuoteSecurity();
        return GetCashAssetBySecurityId(currencyAsset.Id);
    }

    public Asset? GetCashAssetBySecurityId(int securityId, bool isInit = false)
    {
        if (isInit)
            return Portfolio.GetAssetPositionBySecurityId(securityId);
        return Portfolio.GetCashAssetBySecurityId(securityId);
    }
}
