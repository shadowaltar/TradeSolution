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
    private readonly Dictionary<long, Position> _closedPositions = new();
    private readonly Dictionary<int, decimal> _residualByAssetSecurityId = new();

    public Portfolio InitialPortfolio { get; private set; }

    public Portfolio Portfolio { get; private set; }

    public bool HasPosition => Portfolio.HasPosition;

    public bool HasAsset => Portfolio.HasAsset;

    public event Action<Position, Trade>? PositionProcessed;
    public event Action<Position>? PositionCreated;
    public event Action<Position>? PositionUpdated;
    public event Action<List<Position>>? PositionsUpdated;
    public event Action<Position>? PositionClosed;

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

    public List<Position> GetPositions()
    {
        var results = Portfolio.GetPositions();
        results.Sort((r1, r2) => r1.CreateTime.CompareTo(r2.CreateTime));
        return results;
    }

    public List<Position> GetClosedPositions()
    {
        return _closedPositions.ThreadSafeValues();
    }

    public Position? GetPosition(long id)
    {
        return Portfolio.GetPosition(id);
    }

    public Position? GetPositionBySecurityId(int securityId)
    {
        return Portfolio.GetPositionBySecurityId(securityId);
    }

    public Asset? GetAsset(long id)
    {
        return Portfolio.GetAsset(id);
    }

    public List<Asset> GetAssets()
    {
        return Portfolio.GetAssets();
    }

    public Asset? GetAssetBySecurityId(int securityId)
    {
        return Portfolio.GetAssetBySecurityId(securityId);
    }

    public async Task<List<Position>> GetStoragePositions(DateTime? start = null, OpenClose isOpenOrClose = OpenClose.All)
    {
        start ??= DateTime.MinValue;
        var positions = await _context.Storage.ReadPositions(start.Value, isOpenOrClose);
        _securityService.Fix(positions);
        return positions;
    }

    public async Task<List<Asset>> GetExternalAssets()
    {
        var state = await _execution.GetAssetPositions(_context.Account.ExternalAccount);
        if (state.ResultCode == ResultCode.GetAccountFailed)
        {
            _log.Error("Failed to get assets.");
            return new();
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
            Portfolio = new Portfolio(_context.AccountId, new(), new());
            InitialPortfolio = new Portfolio(_context.AccountId, new(), new());
            return true;
        }

        var state = await _execution.Subscribe();

        if (state.ResultCode == ResultCode.SubscriptionFailed)
        {
            return false;
        }

        var positions = await _context.Services.Portfolio.GetStoragePositions(DateTime.MinValue, OpenClose.OpenOnly);
        _securityService.Fix(positions);

        var assets = await _context.Storage.ReadAssets();
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);
        }

        // closedPositions need not be initialized
        // currentPosition by SecurityId should be initialized;
        // must be two different instances
        Portfolio = new Portfolio(_context.AccountId, positions, assets);

        InitialPortfolio = new Portfolio(_context.AccountId,
            positions.Select(p => p with { }).ToList(),
            assets.Select(a => a with { }).ToList());
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

    public async Task<bool> CloseAllOpenPositions(string orderComment)
    {
        // if it is non-fx, create orders to expunge the long/short positions
        var positions = Portfolio.GetPositions();
        if (positions.IsNullOrEmpty()) return false;

        _log.Info($"Closing {positions.Count} opened positions.");
        var count = 0;
        foreach (var position in positions)
        {
            if (position.IsClosed) continue;

            count++;
            var order = CreateCloseOrder(position.Quantity, position.Security, orderComment);
            var state = await _orderService.SendOrder(order);
            // need to wait for a while
            if (Threads.WaitUntil(() => _closedPositions.ThreadSafeContains(position.Id)))
                _log.Info("Closed position, id: " + position.Id);
            else
                _log.Error("Failed to close position (timed-out), id: " + position.Id);
        }
        _persistence.WaitAll();
        return count > 0;
    }

    public async Task<bool> CleanUpNonCashAssets(string orderComment)
    {
        // sell any assets which is not cash / fiat
        var assets = Portfolio.GetAssets();
        if (assets.IsNullOrEmpty()) return false;

        var preferredQuoteCurrency = _context.PreferredQuoteCurrencies.FirstOrDefault();
        if (preferredQuoteCurrency == null)
        {
            _log.Error("Failed to get preferred quote currency.");
            return false;
        }
        // fx only logic
        List<Asset> assetsToBeCleanedUp = !_context.HasCurrencyWhitelist
            ? assets
            : assets.Where(a => _context.CurrencyWhitelist.Contains(a.Security)).ToList();
        if (assetsToBeCleanedUp.IsNullOrEmpty())
            return false;

        _log.Info($"Cleaning up {assetsToBeCleanedUp.Count} assets.");
        var count = 0;
        foreach (var asset in assetsToBeCleanedUp)
        {
            if (preferredQuoteCurrency.Equals(asset.Security)) continue;
            var oldQuoteCurrencyQuantity = Portfolio.GetAssets().FirstOrDefault(a => preferredQuoteCurrency.Equals(a.Security))?.Quantity ?? 0;
            var currencyPair = _securityService.GetFxSecurity(asset.SecurityCode, preferredQuoteCurrency.Code);
            if (currencyPair == null)
            {
                _log.Warn($"Unable to clean up asset position for currency pair (base:{asset.SecurityCode}, quote:{preferredQuoteCurrency}).");
                continue;
            }
            if (asset.IsEmpty)
                continue;

            count++;
            var oldQuantity = asset.Quantity;
            var order = CreateCloseOrder(oldQuantity, currencyPair, orderComment);
            order.Action = OrderActionType.Operational;
            var state = await _orderService.SendOrder(order);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                var r = Threads.WaitUntil(() =>
                {
                    _persistence.WaitAll();
                    var recentAssets = AsyncHelper.RunSync(() => GetStorageAssets());
                    var a = recentAssets.FirstOrDefault(a => a.SecurityCode == asset.SecurityCode);
                    return a?.IsEmpty ?? true; // meaning that the asset is cleaned up
                }, pollingMs: 1000);

                if (r)
                {
                    var quoteCurrencyQuantity = Portfolio.GetAssets().FirstOrDefault(a => a.Security.Equals(preferredQuoteCurrency))?.Quantity ?? 0;

                    _log.Info($"Cleaned up asset {asset.SecurityCode} by selling {currencyPair.Code} with quantity {oldQuantity};" +
                        $" quote currency {preferredQuoteCurrency} quantity {oldQuoteCurrencyQuantity}->{quoteCurrencyQuantity}");
                }
                else
                {
                    _log.Error($"Failed to clean up asset {asset.SecurityCode} (timed-out).");
                }
            }
            else
            {
                _log.Error($"Failed to clean up asset {asset.SecurityCode} (sell order failed).");
            }
        }
        return count > 0;
    }

    public Position? CreateOrApply(Trade trade, Position? position = null)
    {
        if (trade.IsOperational) return position;

        if (position == null)
        {
            var residual = _residualByAssetSecurityId.ThreadSafeGet(trade.Security.FxInfo?.BaseAsset?.Id ?? 0);
            position = Position.Create(trade, residual);
            _securityService.Fix(position); // must call this for min-notional consideration
            if (residual != 0)
                _log.Info($"[{trade.SecurityCode}] has residual quantity {residual} which is merged into a new position with Id: {position.Id}");
        }
        else
        {
            _securityService.Fix(position); // must call this for min-notional consideration
            position.Apply(trade, 0); // residual if exists, only applied during position creation and also placing a close order
        }
        trade.PositionId = position.Id;
        return position;
    }

    public void Update(List<Position> positions, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.ClearPositions();
            Portfolio.ClearPositions();
        }

        foreach (var position in positions)
        {
            _securityService.Fix(position);
            if (position.IsClosed)
            {
                _closedPositions[position.Id] = position;
                Portfolio.RemovePosition(position.Id);
                if (isInitializing)
                    InitialPortfolio.RemovePosition(position.Id);
            }
            else
            {
                Portfolio.AddOrUpdate(position);
            }
            if (isInitializing)
            {
                InitialPortfolio.AddOrUpdate(position with { });
            }
        }
    }

    public void Update(List<Asset> assets, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.ClearAssets();
            Portfolio.ClearAssets();
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

        InitialPortfolio.ClearAssets();
        InitialPortfolio.ClearPositions();
        Portfolio.ClearAssets();
        Portfolio.ClearPositions();

        _closedPositions.Clear();
        _residualByAssetSecurityId.Clear();
    }

    public bool Validate(Order order)
    {
        // TODO
        return true;
    }

    public void SpendAsset(Security security, decimal quantity)
    {
        var asset = Portfolio.GetAssetBySecurityId(security.QuoteSecurity.Id);
        asset.Quantity -= quantity;
    }

    public async Task<Asset> Deposit(int assetId, decimal quantity)
    {
        // TODO external logic!
        var asset = GetAsset(assetId);
        if (asset != null)
        {
            asset.Quantity += quantity;
            // TODO external logic
            var assets = await _storage.ReadAssets();
            asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            return asset;
        }
        throw Exceptions.MissingAsset(assetId);
    }

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
            return await _storage.InsertOne(asset, false) > 0
                ? asset
                : Portfolio.AccountId == accountId ? throw Exceptions.MissingAsset(assetId) : null;
        }
        else
        {
            asset.Quantity += quantity;
            return await _storage.InsertOne(asset, true) > 0
                ? asset
                : Portfolio.AccountId == accountId ? throw Exceptions.MissingAsset(assetId) : null;
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
            var assets = await _storage.ReadAssets();
            asset = assets.FirstOrDefault(b => b.SecurityId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            asset.Quantity -= quantity;
            return asset;
        }
        throw Exceptions.MissingAsset(assetId);
    }

    public async Task Reload(bool clearOnly, bool affectPositions, bool affectAssets, bool affectInitialPortfolio)
    {
        if (affectPositions)
        {
            Portfolio.ClearPositions();
            if (affectInitialPortfolio)
            {
                InitialPortfolio.ClearPositions();
            }
            if (!clearOnly)
            {
                var positions = await _storage.ReadPositions(DateTime.MinValue, OpenClose.OpenOnly);
                foreach (var position in positions)
                {
                    Portfolio.AddOrUpdate(position);
                    if (affectInitialPortfolio)
                    {
                        InitialPortfolio.AddOrUpdate(position);
                    }
                }
            }
        }
        if (affectAssets)
        {
            Portfolio.ClearAssets();
            if (affectInitialPortfolio)
            {
                InitialPortfolio.ClearAssets();
            }
            if (!clearOnly)
            {
                var assets = await _storage.ReadAssets();
                foreach (var asset in assets)
                {
                    Portfolio.AddOrUpdate(asset);
                    if (affectInitialPortfolio)
                    {
                        InitialPortfolio.AddOrUpdate(asset);
                    }
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
        if (trade.IsOperational) return;

        var position = GetPositionBySecurityId(trade.SecurityId);
        var isNew = position == null;
        position = CreateOrApply(trade, position);
        if (position == null) throw Exceptions.Impossible();

        _securityService.Fix(position);

        // update storage for position and position-record
        if (position.IsClosed)
        {
            _closedPositions[position.Id] = position;
            Portfolio.RemovePosition(position.Id);

            if (position.Quantity != 0)
            {
                // has residual, which will be traded in future orders
                var residual = position.Quantity;
                var assetSecId = position.Security.FxInfo?.BaseAsset?.Id ?? 0;
                if (assetSecId != 0)
                {
                    _residualByAssetSecurityId.ThreadSafeSet(position.SecurityId, residual);
                }
                else
                {
                    _log.Warn("Cannot find base asset security Id! Position's security is: " + position.SecurityCode);
                }
            }
        }
        else
        {
            Portfolio.AddOrUpdate(position);
        }
        _persistence.Insert(position);

        // invoke post-events
        if (isNew)
            PositionCreated?.Invoke(position);
        else
            PositionUpdated?.Invoke(position);

        if (position.IsClosed)
        {
            PositionClosed?.Invoke(position);
        }
        PositionProcessed?.Invoke(position, trade);
    }

    public Side GetOpenPositionSide(int securityId)
    {
        var position = GetPositionBySecurityId(securityId);
        return position == null ? Side.None : position.Side;
    }

    public decimal GetAssetPositionResidual(int assetSecurityId)
    {
        return _residualByAssetSecurityId.ThreadSafeGet(assetSecurityId);
    }

    public void ClearCachedClosedPositions(bool isInitializing = false)
    {
        if (isInitializing)
        {
            Clear(InitialPortfolio);
        }
        Clear(Portfolio);

        static void Clear(Portfolio portfolio)
        {
            var openPositions = portfolio.GetPositions().Where(p => !p.IsClosed);
            portfolio.ClearPositions();
            foreach (var position in openPositions)
            {
                portfolio.AddOrUpdate(position);
            }
        }
    }

    private void OnAssetsChanged(List<Asset> assets)
    {
        var account = _context.Account ?? throw Exceptions.MustLogin();
        var states = new List<AssetState>();
        foreach (var asset in assets)
        {
            _securityService.Fix(asset);

            // basically just use the id from the existing item
            var existingAsset = Portfolio.GetAssetBySecurityId(asset.SecurityId);
            asset.AccountId = existingAsset?.AccountId ?? account.Id;
            asset.UpdateTime = DateTime.UtcNow;
            asset.Id = existingAsset != null ? existingAsset.Id : _assetIdGenerator.NewTimeBasedId;
            states.Add(AssetState.From(asset));

            Portfolio.AddOrUpdate(asset);
        }
        _persistence.Insert(assets, isUpsert: true);
        _persistence.Insert(states, isUpsert: false);
    }
}
