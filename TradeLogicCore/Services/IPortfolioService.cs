using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;
public interface IPortfolioService
{
    event Action<Position>? PositionCreated;
    event Action<Position>? PositionUpdated;
    event Action<Position>? PositionClosed;

    Task Initialize();

    List<Position> GetOpenPositions();

    List<Position> GetPositions(string externalName, SecurityType securityType);

    List<Balance> GetExternalBalances(string externalName);

    List<Balance> GetCurrentBalances();

    List<ProfitLoss> GetRealizedPnl(Security security, DateTime rangeStart, DateTime rangeEnd);

    ProfitLoss GetUnrealizedPnl(Security security);
}
