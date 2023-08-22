using TradeCommon.Constants;
using TradeCommon.Database;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Services;
public interface IServices
{
    Context Context { get; }
    Persistence Persistence { get; }
    ExchangeType ExchangeType => Context.ExchangeType;
    BrokerType BrokerType => Context.BrokerType;
    IAdminService Admin { get; }
    IPortfolioService Portfolio { get; }
    IOrderService Order { get; }
    ITradeService Trade { get; }
    ISecurityService Security { get; }
    IHistoricalMarketDataService HistoricalMarketData { get; }
    IMarketDataService MarketData { get; }
}
