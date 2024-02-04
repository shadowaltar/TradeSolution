using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;
public interface IPortfolioService
{
    event Action<Position, Trade>? PositionProcessed;
    event Action<Position>? PositionCreated;
    event Action<Position>? PositionUpdated;
    event Action<Position>? PositionClosed;

    Portfolio InitialPortfolio { get; }

    Portfolio Portfolio { get; }

    bool HasPosition { get; }

    bool HasAsset { get; }

    /// <summary>
    /// Initialize portfolio service.
    /// Expected to be executed after account and user are specified
    /// (usually via <see cref="IAdminService.Login(string, string?, string?, TradeCommon.Runtime.EnvironmentType)"/>).
    /// </summary>
    /// <returns></returns>
    Task<bool> Initialize();

    Task Reset();
    
    /// <summary>
    /// Create or update (and cache) one or more positions by a series of trades.
    /// </summary>
    /// <param name="trades"></param>
    /// <param name="isSameSecurity"></param>
    /// <returns></returns>
    void Process(List<Trade> trades, bool isSameSecurity);

    /// <summary>
    /// Create or update (and cache) one position by one trade.
    /// </summary>
    /// <param name="trade"></param>
    void Process(Trade trade);

    /// <summary>
    /// Gets all open positions (a shallow copy of list of positions).
    /// </summary>
    /// <returns></returns>
    List<Position> GetPositions();

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

    Side GetOpenPositionSide(int securityId);

    /// <summary>
    /// Get all asset positions.
    /// </summary>
    /// <returns></returns>
    List<Asset> GetAssets();

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
    /// Spend the free quantity in the related asset position given a security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    void SpendAsset(Security security, decimal quantity);

    ///// <summary>
    ///// Realize the pnl from the trade just closed, related to a specific security;
    ///// then set the quantity and notional value of its related asset position.
    ///// Returns the new total realized pnl of this security.
    ///// </summary>
    ///// <param name="security"></param>
    ///// <param name="realizedPnl"></param>
    ///// <returns></returns>
    //decimal Realize(Security security, decimal realizedPnl);

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
    /// Traverse through current position and non-basic assets,
    /// create corresponding opposite side orders and send.
    /// </summary>
    Task<bool> CloseAllAssets(string orderComment);

    Task<bool> CleanUpNonCashAssets(string orderComment);

    /// <summary>
    /// Create a position by a trade, or apply the trade into the given position.
    /// If the trade is operational, it will not be processed
    /// </summary>
    /// <param name="trade"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    Position? CreateOrApply(Trade trade, Position? position = null);

    /// <summary>
    /// Get positions from storage.
    /// Optionally can specify the account which the positions belong to, and
    /// the lower bound (inclusive) of position update time.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="isOpenOrClose"></param>
    /// <returns></returns>
    Task<List<Position>> GetStoragePositions(DateTime? start = null, OpenClose isOpenOrClose = OpenClose.All);

    Task<List<Asset>> GetExternalAssets();

    Task<List<Asset>> GetStorageAssets();

    Task<List<AssetState>> GetAssetStates(Security security, DateTime start);

    void Update(List<Asset> assets, bool isInitializing = false);

    void Update(List<Position> positions, bool isInitializing = false);

    /// <summary>
    /// Reload service cache.
    /// For positions, only open positions may be cached.
    /// For assets, all asset positions will be cached.
    /// Can either clear the cache only, or clear + reload from storage.
    /// Can only affect position, asset, or both (or none, though useless).
    /// Can also affect initial portfolio object.
    /// </summary>
    /// <param name="clearOnly"></param>
    /// <param name="affectPositions"></param>
    /// <param name="affectAssets"></param>
    /// <param name="affectInitialPortfolio"></param>
    Task Reload(bool clearOnly, bool affectPositions, bool affectAssets, bool affectInitialPortfolio);

    void ClearCachedClosedPositions(bool isInitializing = false);

    decimal GetAssetPositionResidual(int assetSecurityId);
}
