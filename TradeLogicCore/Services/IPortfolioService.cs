using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;
public interface IPortfolioService
{
    event Action<Position>? PositionCreated;
    event Action<Position>? PositionUpdated;
    event Action<Position>? PositionClosed;

    Portfolio InitialPortfolio { get; }

    Portfolio Portfolio { get; }

    /// <summary>
    /// Initialize portfolio service.
    /// Expected to be executed after account and user are specified
    /// (usually via <see cref="IAdminService.Login(string, string?, string?, TradeCommon.Runtime.EnvironmentType)"/>).
    /// </summary>
    /// <returns></returns>
    Task<bool> Initialize();

    /// <summary>
    /// Create or update (and cache) a position by a trade.
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="existing"></param>
    /// <returns></returns>
    Position CreateOrUpdate(Trade trade, Position? existing = null);

    /// <summary>
    /// Create or update (and cache) one or more positions by a series of trades.
    /// </summary>
    /// <param name="trades"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    Position? CreateOrUpdate(List<Trade> trades, Position? position = null);

    /// <summary>
    /// Apply a trade to an existing position.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="trade"></param>
    void Apply(Position position, Trade trade);

    List<Position> GetOpenPositions();

    /// <summary>
    /// Get position given its Id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Position? GetPosition(long id);

    /// <summary>
    /// Get position given its security Id.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Position? GetPositionBySecurityId(int securityId);

    /// <summary>
    /// Get asset position given its Id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Asset? GetAsset(long id);

    /// <summary>
    /// Get asset position given its security Id.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Asset? GetAssetBySecurityId(int securityId);

    /// <summary>
    /// Get asset's position given a position's security ID.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Asset GetPositionRelatedQuoteBalance(int securityId);

    /// <summary>
    /// Spend the free quantity in the related asset position given a security Id.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    void SpendAsset(int securityId, decimal quantity);

    /// <summary>
    /// Realize the pnl from the trade just closed, related to a specific security;
    /// then set the quantity and notional value of its related asset position.
    /// Returns the new total realized pnl of this security.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="realizedPnl"></param>
    /// <returns></returns>
    decimal Realize(int securityId, decimal realizedPnl);

    List<Position> GetPositions();

    decimal GetRealizedPnl(Security security);

    ProfitLoss GetUnrealizedPnl(Security security);

    bool Validate(Order order);

    /// <summary>
    /// Deposit assets to current account's specific asset indicated by <paramref name="assetId"/>.
    /// If asset is not found, throws exception.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    Task<Asset?> Deposit(int assetId, decimal quantity);

    /// <summary>
    /// Deposit assets to specific account's specific asset indicated by <paramref name="accountId"/> and <paramref name="assetId"/>.
    /// If targeting current account and asset is not found, throws exception; otherwise returns null.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="assetId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    Task<Asset?> Deposit(int accountId, int assetId, decimal quantity);

    Task<Asset?> Withdraw(int assetId, decimal quantity);

    /// <summary>
    /// Create an opposite side order from a known position.
    /// Since the quote/currency asset may not be the same as before,
    /// an overriding security can be provided.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="security"></param>
    /// <returns></returns>
    Order CreateCloseOrder(Asset position, Security? security = null);

    /// <summary>
    /// Traverse through current position and non-basic assets,
    /// create corresponding opposite side orders and send.
    /// </summary>
    Task CloseAllPositions();

    Task<List<Asset>> GetAssets(Account account, bool requestExternal = false);
    void Update(List<Asset> assets, bool isInitializing = false);
    void Update(List<Position> positions, bool isInitializing = false);
}
