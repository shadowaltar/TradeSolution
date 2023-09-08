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

    Task Initialize();

    List<Position> GetOpenPositions();

    /// <summary>
    /// Get position given its security ID.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Position? GetPosition(int securityId);

    /// <summary>
    /// Get asset's positoin given its asset (security) ID.
    /// </summary>
    /// <param name="assetId"></param>
    /// <returns></returns>
    Position? GetAsset(int assetId);

    /// <summary>
    /// Realize the pnl from the trade just closed, related to a specific security.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="tradeRealizedPnl"></param>
    /// <returns></returns>
    decimal Realize(int securityId, decimal tradeRealizedPnl);

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

    Task<bool> Deposit(int assetId, decimal quantity);

    Task<bool> Withdraw(int assetId, decimal quantity);
}
