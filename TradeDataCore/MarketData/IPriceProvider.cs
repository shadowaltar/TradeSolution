using TradeCommon.Essentials;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public interface IPriceProvider
{
    event Action<long, IntervalType, OhlcPrice>? NextPrice;
}
