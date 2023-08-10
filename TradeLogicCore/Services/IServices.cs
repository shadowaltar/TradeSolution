using TradeCommon.Constants;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public interface IServices
{
    Context Context { get; }
    ExchangeType ExchangeType => Context.ExchangeType;
    BrokerType BrokerType => Context.BrokerType;
    IAdminService Admin { get; }
    IPortfolioService Portfolio { get; }
    IOrderService Order { get; }
    ITradeService Trade { get; }
    ISecurityService Security { get; }
    IHistoricalMarketDataService HistoricalMarketData { get; }
    IRealTimeMarketDataService RealTimeMarketData { get; }
}
