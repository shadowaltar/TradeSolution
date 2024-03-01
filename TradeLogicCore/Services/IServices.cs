using TradeCommon.Database;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public interface IServices
{
    Context Context { get; }
    Persistence Persistence { get; }
    DataPublisher Publisher { get; }
    IAdminService Admin { get; }
    IAlgorithmService Algo { get; }
    IPortfolioService Portfolio { get; }
    IOrderService Order { get; }
    ITradeService Trade { get; }
    ISecurityService Security { get; }
    IHistoricalMarketDataService HistoricalMarketData { get; }
    IMarketDataService MarketData { get; }

    Task Reset();
}
