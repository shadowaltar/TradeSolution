using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalQuotationManagement
{
    string Name { get; }

    Task<ExternalConnectionState> InitializeAsync();

    Task<ExternalConnectionState> SubscribeAsync(Security security);

    Task<ExternalConnectionState> UnsubscribeAsync(Security security);
}
