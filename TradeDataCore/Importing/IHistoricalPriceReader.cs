using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.Importing;

public interface IHistoricalPriceReader
{
    Task<Dictionary<long, List<OhlcPrice>>?> ReadPrices(List<Security> securities, DateTime start, DateTime end, IntervalType intervalType);
}