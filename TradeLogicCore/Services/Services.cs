using Common;
using TradeCommon.Constants;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public class Services : IServices
{
    public Services(string brokerName,
        Context context,
        IAdminService admin,
        IPortfolioService portfolio,
        IOrderService order,
        ITradeService trade,
        ISecurityService security,
        IHistoricalMarketDataService historicalMarketDataService,
        IRealTimeMarketDataService realTimeMarketDataService)
    {
        BrokerType = brokerName.ConvertDescriptionToEnum<BrokerType>();
        ExchangeType = ExternalNames.Convert(BrokerType);

        Context = context;
        Admin = admin;
        Portfolio = portfolio;
        Order = order;
        Trade = trade;
        Security = security;
        HistoricalMarketData = historicalMarketDataService;
        RealTimeMarketData = realTimeMarketDataService;
    }

    public Context Context { get; private set; }

    public IAdminService Admin { get; }

    public ExchangeType ExchangeType { get; private set; }

    public BrokerType BrokerType { get; private set; }

    public IPortfolioService Portfolio { get; private set; }

    public IOrderService Order { get; private set; }

    public ITradeService Trade { get; private set; }

    public ISecurityService Security { get; private set; }

    public IHistoricalMarketDataService HistoricalMarketData { get; private set; }

    public IRealTimeMarketDataService RealTimeMarketData { get; private set; }
}
