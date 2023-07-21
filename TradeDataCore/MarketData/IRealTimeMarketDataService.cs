using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public interface IRealTimeMarketDataService
{
    Task<ExternalConnectionState> SubscribeOhlcAsync(Security security);
    Task<ExternalConnectionState> UnsubscribeOhlcAsync(Security security);
    Task<ExternalConnectionState> UnsubscribeAllOhlcsAsync();

    Task<ExternalConnectionState> SubscribeTickAsync(Security security);
    Task<ExternalConnectionState> UnsubscribeTickAsync(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicksAsync();
}
