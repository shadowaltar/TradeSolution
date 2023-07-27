using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalQuotationManagement
{
    string Name { get; }

    public event Action<int, OhlcPrice>? NewOhlc;

    Task<ExternalConnectionState> Initialize();

    ExternalConnectionState SubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown);

    Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown);
}
