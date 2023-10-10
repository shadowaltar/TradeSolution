using Common;
using log4net;
using Microsoft.IdentityModel.Tokens;
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
    private readonly IdGenerator _assetIdGenerator;
    private readonly Context _context;
    private readonly IExternalExecutionManagement _execution;
    private readonly IStorage _storage;
    private readonly IOrderService _orderService;
    private readonly ISecurityDefinitionProvider _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, Position> _closedPositions = new();
    private readonly object _lock = new();

    public Portfolio InitialPortfolio { get; private set; }

    public Portfolio Portfolio { get; private set; }

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
        _execution.AssetsChanged -= OnBalancesChanged;
        _execution.AssetsChanged += OnBalancesChanged;

        _orderIdGenerator = IdGenerators.Get<Order>();
        _assetIdGenerator = IdGenerators.Get<Asset>();
    }

    private void OnBalancesChanged(List<Asset> assets)
    {
        var account = _context.Account ?? throw Exceptions.MustLogin();
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset))
            {
                _log.Error("Received an asset update with unknown asset id, asset code is: " + asset.SecurityCode);
                continue;
            }

            asset.AccountId = account.Id;
            asset.UpdateTime = DateTime.UtcNow;
        }
        _persistence.Insert(assets, isUpsert: true);
    }

    //private void OnNewTrades(List<Trade> trades)
    //{
    //    foreach (var trade in trades)
    //    {
    //        OnNewTrade(trade);
    //    }
    //}

    //private void OnNewTrade(Trade trade)
    //{
    //    // one security to one position
    //    var securityId = trade.SecurityId;
    //    var position = Portfolio.GetPositionBySecurityId(securityId);
    //    // either create a new position, or merge the trade into it
    //    if (position != null)
    //    {
    //        position.Apply(trade);

    //        if (position.IsClosed)
    //        {
    //            Portfolio.RemovePosition(position.Id);
    //            _closedPositions.ThreadSafeSet(position.Id, position, _lock);
    //            PositionClosed?.Invoke(position);
    //        }
    //        else
    //        {
    //            PositionUpdated?.Invoke(position);
    //        }
    //    }
    //    else
    //    {
    //        var orderId = trade.OrderId;
    //        // must be the order to open a position
    //        var order = _orderService.GetOrder(orderId);
    //        if (order == null)
    //        {
    //            // a trade without an order which should not happen
    //            _log.Error("A trade comes without an associated order!");
    //            return;
    //        }
    //        position = Position.Create(trade);
    //        PositionCreated?.Invoke(position);
    //    }
    //    // update existing, or save a new position to db
    //    _persistence.Insert(position);
    //}

    public List<Position> GetPositions()
    {
        var results = Portfolio.GetPositions();
        results.Sort((r1, r2) => r1.CreateTime.CompareTo(r2.CreateTime));
        return results;
    }

    public List<Position> GetClosedPositions()
    {
        return _closedPositions.ThreadSafeValues(_lock);
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

    public Asset? GetAssetBySecurityId(int securityId)
    {
        return Portfolio.GetAssetBySecurityId(securityId);
    }

    public async Task<List<Position>> GetStoragePositions(Account? account = null, DateTime? start = null)
    {
        account ??= _context.Account;
        start ??= DateTime.MinValue;
        return await _context.Storage.ReadPositions(account, start.Value, false);
    }

    public async Task<List<Asset>> GetExternalAssets(Account? account = null)
    {
        account ??= _context.Account;
        var state = await _execution.GetAssetPositions(account.ExternalAccount);
        var assets = state.Get<List<Asset>>()!;
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset))
                continue;
        }
        return assets.Where(a => !a.IsSecurityInvalid()).ToList();
    }

    public async Task<List<Asset>> GetStorageAssets(Account? account = null)
    {
        account ??= _context.Account;
        var assets = await _storage.ReadAssets(account.Id);
        foreach (var asset in assets)
        {
            if (!_securityService.Fix(asset))
                continue;
        }
        return assets.Where(a => !a.IsSecurityInvalid()).ToList();
    }

    public async Task<bool> Initialize()
    {
        if (_context.Account == null) throw Exceptions.MustLogin();
        var state = await _execution.Subscribe();

        if (state.ResultCode == ResultCode.SubscriptionFailed)
        {
            return false;
        }

        var positions = await _context.Storage.ReadPositions(_context.Account, DateTime.MinValue, true);
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
        Portfolio = new Portfolio(_context.AccountId, positions, assets);
        InitialPortfolio = Portfolio with { };
        return true;
    }

    public Order CreateCloseOrder(Asset position, string comment, Security? securityOverride = null)
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

            Comment = comment,
        };
    }

    public async Task CloseAllOpenPositions(string orderComment)
    {
        // if it is non-fx, create orders to expunge the long/short positions
        var positions = Portfolio.GetPositions();
        if (positions.IsNullOrEmpty()) return;

        foreach (var position in positions)
        {
            if (position.IsClosed) continue;

            var order = CreateCloseOrder(position, orderComment);
            await _orderService.SendOrder(order);
            // need to wait for a while to 
            Threads.WaitUntil(() => Portfolio.GetPosition(position.Id)?.EndOrderId == order.Id, pollingMs: 1000);
        }
        _context.Services.Persistence.WaitAll();

        //// if it is fx+crypto, sell all non-basic assets if previously traded
        //var assets = Portfolio.GetAssets();
        //foreach (var asset in assets)
        //{
        //    if (_context.PreferredAssetCodes.Contains(asset.SecurityCode))
        //        continue;

        //    // cannot close an asset directly, need to pair it up with a quote asset
        //    // eg. to close BTC position, with known preferred assets USDT and TUSD,
        //    // will try to use BTCUSDT to close position, then BTCTUSD if BTCUSDT is not available
        //    Security? security = null;
        //    var codes = _context.PreferredAssetCodes.Select(c => asset.SecurityCode + c).ToList();
        //    foreach (var code in codes)
        //    {
        //        security = _securityService.GetSecurity(code);
        //        if (security != null) break;
        //    }

        //    if (security != null)
        //    {
        //        var order = CreateCloseOrder(asset, orderComment, security);
        //        await _orderService.SendOrder(order);
        //    }

        //    // TEMP TODO
        //    break;
        //}
    }

    public void UpdatePortfolio(List<Position> positions, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.ClearPositions();
            Portfolio.ClearPositions();
        }

        foreach (var position in positions)
        {
            if (!_securityService.Fix(position))
                continue;
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
                InitialPortfolio.AddOrUpdate(position);
            }
        }
    }

    public void UpdatePortfolio(List<Asset> assets, bool isInitializing = false)
    {
        if (isInitializing)
        {
            InitialPortfolio.ClearAssets();
            Portfolio.ClearAssets();
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
                InitialPortfolio.Add(asset);
            }
            Portfolio.Add(asset);
        }
    }

    public void Dispose()
    {
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

    //public decimal Realize(Security security, decimal realizedPnl)
    //{
    //    var position = Portfolio.GetPositionBySecurityId(security.Id);
    //    if (position != null)
    //    {
    //        position.RealizedPnl += realizedPnl;

    //        var asset = Portfolio.GetAssetBySecurityId(security.QuoteSecurity.Id);
    //        if (asset == null) throw Exceptions.MissingAssetPosition(security);
    //        asset.Quantity += realizedPnl;

    //        return position.RealizedPnl;
    //    }
    //    return decimal.MinValue;
    //}

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

    public void Process(List<Trade> trades, bool isSameSecurity)
    {
        if (trades.IsNullOrEmpty())
            return;

        if (isSameSecurity)
        {
            var lastTrade = trades.Last();
            var position = GetPositionBySecurityId(lastTrade.SecurityId);
            var isNew = position == null;
            var positions = Position.CreateOrApply(trades, position).ToList();
            if (positions.IsNullOrEmpty()) throw Exceptions.Impossible();

            foreach (var p in positions)
            {
                if (p.IsClosed)
                {
                    _closedPositions[p.Id] = p;
                    Portfolio.RemovePosition(p.Id);
                }
            }
            _persistence.Insert(positions);
            _persistence.Insert(positions[^1].CreateRecord());

            // invoke post-events
            PositionsUpdated?.Invoke(positions);
        }
        else
        {
            foreach (var grouped in trades.GroupBy(t => t.SecurityId))
            {
                Process(grouped.ToList(), true);
            }
        }
    }

    public void Process(Trade trade)
    {
        var position = GetPositionBySecurityId(trade.SecurityId);
        var isNew = position == null;
        position = Position.CreateOrApply(trade, position);
        if (position == null) throw Exceptions.Impossible();

        // update storage for position and position-record
        if (position.IsClosed)
        {
            _closedPositions[position.Id] = position;
            Portfolio.RemovePosition(position.Id);
        }
        _persistence.Insert(position);
        _persistence.Insert(position.CreateRecord());

        // invoke post-events
        if (isNew)
            PositionCreated?.Invoke(position!);
        else
            PositionUpdated?.Invoke(position!);
        if (position.IsClosed)
            PositionClosed?.Invoke(position!);
    }
}
