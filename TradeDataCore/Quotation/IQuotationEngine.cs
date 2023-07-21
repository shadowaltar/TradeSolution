using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeDataCore.Quotation;
public interface IQuotationEngine
{
    Task<ExternalConnectionState> InitializeAsync();

    Task<ExternalConnectionState> SubscribeAsync(Security security);

    Task<ExternalConnectionState> UnsubscribeAsync(Security security);
}
