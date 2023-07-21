using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public interface IHistoricalMarketDataService
{
    List<OhlcPrice> Get(Security security, DateTime start, DateTime end);
    List<Tick> GetTicks(Security security, DateTime start, DateTime end);
    List<IStockCorporateAction> GetCorporateActions(Security security, DateTime start, DateTime end);
    List<FinancialStats> GetFundamentals(Security security, DateTime start, DateTime end);
}
