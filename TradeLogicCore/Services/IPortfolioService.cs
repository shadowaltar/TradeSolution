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
    Task Initialize();

    List<Position> GetOpenPositions();

    /// <summary>
    /// Get position given its security ID.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Position? GetPosition(int securityId);

    /// <summary>
    /// Get asset's position given its asset (security) ID.
    /// </summary>
    /// <param name="assetId"></param>
    /// <returns></returns>
    Position GetAssetPosition(int assetId);

    /// <summary>
    /// Get asset's position given a position's security ID.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Position GetPositionRelatedCurrencyAsset(int securityId);

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

    /// <summary>
    /// TODO revise
    /// </summary>
    /// <param name="externalName"></param>
    /// <returns></returns>
    List<Balance> GetExternalBalances(string externalName);

    decimal GetRealizedPnl(Security security);

    ProfitLoss GetUnrealizedPnl(Security security);

    bool Validate(Order order);

    /// <summary>
    /// Deposit assets to current account's specific balance indicated by <paramref name="assetId"/>.
    /// If asset is not found, throws exception.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    Task<Balance?> Deposit(int assetId, decimal quantity);

    /// <summary>
    /// Deposit assets to specific account's specific balance indicated by <paramref name="accountId"/> and <paramref name="assetId"/>.
    /// If targeting current account and asset is not found, throws exception; otherwise returns null.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="assetId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    Task<Balance?> Deposit(int accountId, int assetId, decimal quantity);

    Task<Balance?> Withdraw(int assetId, decimal quantity);
}
