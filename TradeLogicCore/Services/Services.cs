using TradeCommon.Database;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public class Services(Context context,
                      Persistence persistence,
                      IAdminService admin,
                      IAlgorithmService algo,
                      IPortfolioService portfolio,
                      IOrderService order,
                      ITradeService trade,
                      ISecurityService security,
                      IHistoricalMarketDataService historicalMarketDataService,
                      IMarketDataService marketDataService,
                      DataPublisher publisher) : IServices
{
    public Context Context { get; } = context;

    public Persistence Persistence { get; } = persistence;

    public IAdminService Admin { get; } = admin;
    
    public IAlgorithmService Algo { get; } = algo;
    
    public IPortfolioService Portfolio { get; private set; } = portfolio;

    public IOrderService Order { get; private set; } = order;

    public ITradeService Trade { get; private set; } = trade;

    public ISecurityService Security { get; private set; } = security;

    public IHistoricalMarketDataService HistoricalMarketData { get; private set; } = historicalMarketDataService;

    public IMarketDataService MarketData { get; private set; } = marketDataService;

    public DataPublisher Publisher { get; } = publisher;

    public async Task Reset()
    {
        Persistence.WaitAll();
        Publisher.Reset();

        await MarketData.Reset();
        await Portfolio.Reset();
        Trade.Reset();
        Order.Reset();
        Security.Reset();
    }
}
