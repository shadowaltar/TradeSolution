using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public interface IRealTimeMarketDataService
{
    event Action<int, OhlcPrice> NextOhlc;
    event Action<int, Tick> NextTick;

    IExternalQuotationManagement External { get; }

    Task Initialize();
    Task<ExternalConnectionState> SubscribeOhlc(Security security);
    Task<ExternalConnectionState> UnsubscribeOhlc(Security security);
    Task<ExternalConnectionState> UnsubscribeAllOhlcs();

    Task<ExternalConnectionState> SubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeTick(Security security);
    Task<ExternalConnectionState> UnsubscribeAllTicks();
}