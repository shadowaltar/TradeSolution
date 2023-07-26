using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.PortfolioManagement;
public interface IPortfolioEngine
{
    Task Initialize();

    Task<bool> ConfirmTrade(Trade trade);

    List<Position> GetAllPositions();

    List<Position> GetPositions(string externalName, SecurityType securityType);

    List<Balance> GetExternalBalances(string externalName);

    List<Balance> GetCurrentBalances();

    List<ProfitLoss> GetRealizedPnl(Security security, DateTime rangeStart, DateTime rangeEnd);
    
    ProfitLoss GetUnrealizedPnl(Security security);

    Task Persist();
}
