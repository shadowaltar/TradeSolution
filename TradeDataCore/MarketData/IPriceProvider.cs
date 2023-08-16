using TradeCommon.Essentials;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;

public interface IPriceProvider
{
    event Action<int, IntervalType, OhlcPrice>? NextPrice;
}
