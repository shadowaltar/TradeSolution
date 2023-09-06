using TradeCommon.Database;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public interface IServices
{
    Persistence Persistence { get; }
    IAdminService Admin { get; }
    IPortfolioService Portfolio { get; }
    IOrderService Order { get; }
    ITradeService Trade { get; }
    ISecurityService Security { get; }
    IHistoricalMarketDataService HistoricalMarketData { get; }
    IMarketDataService MarketData { get; }
}
