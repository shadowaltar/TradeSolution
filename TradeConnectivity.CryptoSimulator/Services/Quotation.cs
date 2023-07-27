using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.CryptoSimulator.Services;
public class Quotation : IExternalQuotationManagement
{
    public string Name => ExternalNames.CryptoSimulator;

    public Task<ExternalConnectionState> InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> SubscribeAsync(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAsync(Security security)
    {
        throw new NotImplementedException();
    }

    private void Process()
    {

    }
}
