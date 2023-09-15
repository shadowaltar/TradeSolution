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
    private readonly IExternalExecutionManagement _execution;
    private readonly Context _context;
    private readonly IStorage _storage;
    private readonly IOrderService _orderService;
    private readonly ITradeService _tradeService;
    private readonly ISecurityDefinitionProvider _securityService;
    private readonly Persistence _persistence;
    private readonly Dictionary<long, long> _orderToPositionIds = new();
    private readonly Dictionary<long, Position> _openPositions = new();
    private readonly Dictionary<long, Position> _closedPositions = new();
    private readonly object _lock = new();

    public Portfolio InitialPortfolio { get; private set; }

    public Portfolio Portfolio { get; private set; }

    public event Action<Position>? PositionCreated;
    public event Action<Position>? PositionUpdated;
    public event Action<Position>? PositionClosed;

    public PortfolioService(IExternalExecutionManagement externalExecution,
                            Context context,
                            IOrderService orderService,
                            ITradeService tradeService,
                            ISecurityService securityService,
                            Persistence persistence)
    {
        _execution = externalExecution;
        _context = context;
        _storage = context.Storage;
        _orderService = orderService;
        _tradeService = tradeService;
        _securityService = securityService;
        _persistence = persistence;

        _tradeService.NextTrade += OnNewTrade;

        _orderIdGenerator = IdGenerators.Get<Order>();
        _positionIdGenerator = IdGenerators.Get<Position>();
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

        bool positionExists = false;
        Position? position;
        lock (_lock)
        {
            positionExists = _openPositions.TryGetValue(positionId, out position);
        }

        // either create a new position, or merge the trade into it
        if (positionExists && position != null)
        {
            Merge(position, trade);

            if (position.IsClosed)
            {
                lock (_lock)
                {
                    _openPositions.Remove(positionId);
                    _closedPositions[positionId] = position;
                }
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
            position.Orders.Add(order);

            PositionCreated?.Invoke(position);

            _persistence.Enqueue(new PersistenceTask<Position>(position));
        }
    }

    public List<Position> GetOpenPositions()
    {
        List<Position> results;
        lock (_lock)
        {
            results = _openPositions.Values.OrderBy(p => p.StartTime).ToList();
        }
        return results;
    }

    public List<Position> GetClosedPositions()
    {
        List<Position> results;
        lock (_lock)
        {
            results = _closedPositions.Values.ToList();
        }
        return results;
    }

    public List<Balance> GetCurrentBalances()
    {
        throw new NotImplementedException();
    }

    public List<Balance> GetExternalBalances(string externalName)
    {
        throw new NotImplementedException();
    }

    public Position? GetPosition(int securityId)
    {
        return Portfolio.Positions!.GetOrDefault(securityId);
    }

    public Position GetAssetPosition(int assetId)
    {
        return Portfolio.AssetPositions!.GetOrDefault(assetId) ?? throw Exceptions.MissingAssetPosition(assetId.ToString());
    }

    public Position GetPositionRelatedCurrencyAsset(int securityId)
    {
        var security = _securityService.GetSecurity(securityId);
        var currencyAsset = security.EnsureCurrencyAsset();
        return GetAssetPosition(currencyAsset.Id) ?? throw Exceptions.MissingAssetPosition(currencyAsset.Code);
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

    public async Task Initialize()
    {
        var account = _context.Account
            ?? throw new InvalidOperationException("Must login an account before initializing portfolio");

        await _execution.Subscribe();

        // must be two different instances
        InitialPortfolio = new Portfolio(account);
        Portfolio = new Portfolio(account);
    }

    /// <summary>
    /// Creates a position entry by a trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    public Position Create(Trade trade)
    {
        var position = new Position
        {
            Id = _positionIdGenerator.NewInt,
            SecurityId = trade.SecurityId,
            StartTime = trade.Time,
            UpdateTime = trade.Time,
            Quantity = trade.Quantity,
            Price = trade.Price,
            RealizedPnl = 0,
            Orders = new(),
            Trades = new(),
        };
        position.Trades.Add(trade);
        return position;
    }

    /// <summary>
    /// Merges a trade entry into the corresponding position.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="trade"></param>
    public void Merge(Position position, Trade trade)
    {
        if (position.SecurityId != trade.SecurityId)
        {
            _log.Error($"Must merge a trade into a position with the same security Id (t:{trade.SecurityId} vs p:{position.SecurityId}).");
            return;
        }

        var newQuantity = position.Quantity + (((int)trade.Side) * trade.Quantity);
        var oldValue = position.Price * position.Quantity;
        var tradeValue = trade.Price * trade.Quantity;
        var newValue = oldValue + (((int)trade.Side) * tradeValue);

        var newPrice = position.Price;
        var newPnl = 0m;
        if (Math.Sign(position.Quantity) != (int)trade.Side)
        {
            // decreasing the size of position, so the average price should not change
            newPnl = tradeValue - (position.Price * trade.Quantity);
        }
        else
        {
            // increasing the size of position, so no new Realized Pnl
            newPrice = newValue / newQuantity;
        }

        position.UpdateTime = trade.Time;
        position.Quantity = newQuantity;
        position.Price = newPrice;
        position.RealizedPnl += newPnl;
        position.Trades.Add(trade);
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
        var assetPosition = GetPositionRelatedCurrencyAsset(securityId);
        assetPosition.Quantity -= quantity;
        assetPosition.Notional -= quantity;
    }

    public decimal Realize(int securityId, decimal realizedPnl)
    {
        if (Portfolio.Positions.TryGetValue(securityId, out var position))
        {
            position.RealizedPnl += realizedPnl;

            var assetPosition = GetPositionRelatedCurrencyAsset(securityId);
            assetPosition.Quantity += realizedPnl;
            assetPosition.Notional += realizedPnl;

            return position.RealizedPnl;
        }
        return decimal.MinValue;
    }

    public async Task<Balance> Deposit(int assetId, decimal quantity)
    {
        // TODO external logic!
        var asset = GetAssetPosition(assetId);
        if (asset != null)
        {
            asset.Quantity += quantity;
            // TODO external logic

            var balances = await _storage.ReadBalances(Portfolio.AccountId);
            var balance = balances.FirstOrDefault(b => b.AssetId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            return balance;
        }
        throw Exceptions.MissingAssetPosition(assetId);
    }

    public async Task<Balance?> Deposit(int accountId, int assetId, decimal quantity)
    {
        // TODO external logic!
        var balances = await _storage.ReadBalances(accountId);
        var balance = balances.FirstOrDefault(b => b.AssetId == assetId);
        if (balance == null)
        {
            balance = new Balance
            {
                AccountId = accountId,
                AssetCode = _securityService.GetSecurity(assetId).Code,
                AssetId = assetId,
                FreeAmount = quantity,
                LockedAmount = 0,
                SettlingAmount = 0,
                UpdateTime = DateTime.UtcNow,
            };
            if (await _storage.InsertBalance(balance, false) > 0)
                return balance;
            else
            {
                if (Portfolio.AccountId == accountId)
                    throw Exceptions.MissingAssetPosition(assetId);
                else
                    return null;
            }
        }
        else
        {
            balance.FreeAmount += quantity;
            if (await _storage.InsertBalance(balance, true) > 0)
                return balance;
            else
            {
                if (Portfolio.AccountId == accountId)
                    throw Exceptions.MissingAssetPosition(assetId);
                else
                    return null;
            }
        }
    }

    public async Task<Balance?> Withdraw(int assetId, decimal quantity)
    {
        // TODO external logic!
        var asset = GetAssetPosition(assetId);
        if (asset != null)
        {
            if (asset.Quantity < quantity)
            {
                _log.Error($"Attempt to withdraw quantity more than the free amount. Requested: {quantity}, free amount: {asset.Quantity}.");
                return null;
            }
            asset.Quantity -= quantity;
            _log.Info($"Withdrew {quantity} quantity from current account. Remaining free amount: {asset.Quantity}.");
            var balances = await _storage.ReadBalances(Portfolio.AccountId);
            var balance = balances.FirstOrDefault(b => b.AssetId == assetId) ?? throw Exceptions.MissingBalance(Portfolio.AccountId, assetId);
            balance.FreeAmount -= quantity;
            return balance;
        }
        throw Exceptions.MissingAssetPosition(assetId);
    }
}
