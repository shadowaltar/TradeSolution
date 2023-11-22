using TradeCommon.Database;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public class Services : IServices
{
    public Services(Context context,
                    Persistence persistence,
                    IAdminService admin,
                    IPortfolioService portfolio,
                    IOrderService order,
                    ITradeService trade,
                    ISecurityService security,
                    IHistoricalMarketDataService historicalMarketDataService,
                    IMarketDataService marketDataService,
                    DataPublisher publisher)
    {
        Context = context;
        Persistence = persistence;
        Admin = admin;
        Portfolio = portfolio;
        Order = order;
        Trade = trade;
        Security = security;
        HistoricalMarketData = historicalMarketDataService;
        MarketData = marketDataService;
        Publisher = publisher;
    }

    public Context Context { get; }

    public Persistence Persistence { get; }

    public IAdminService Admin { get; }

    public IPortfolioService Portfolio { get; private set; }

    public IOrderService Order { get; private set; }

    public ITradeService Trade { get; private set; }

    public ISecurityService Security { get; private set; }

    public IHistoricalMarketDataService HistoricalMarketData { get; private set; }

    public IMarketDataService MarketData { get; private set; }

    public DataPublisher Publisher { get; }

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
