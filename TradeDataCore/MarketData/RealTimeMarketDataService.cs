using Autofac;
using Common;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Quotation;

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IRealTimeMarketDataService
{
    private readonly IExternalQuotationManagement _quotationEngine;

    public RealTimeMarketDataService(IExternalQuotationManagement quotationEngine)
    {
        _quotationEngine = quotationEngine;
    }

    public async Task<ExternalConnectionState> SubscribeOhlcAsync(Security security)
    {
        var externalNames = MarketDataSources.GetExternalNames(security);
        if (externalNames.IsNullOrEmpty())
        {
            return new ExternalConnectionState
            {
                Action = ConnectionActionType.Subscribe,
                StatusCode = nameof(StatusCodes.InvalidArgument),
                ExternalPartyId = security.Exchange,
                Description = "Unknown combination of security type, sub-type and exchange name: " + security,
                Type = SubscriptionType.RealTimeMarketData,
                UniqueConnectionId = "",
            };
        }

        string? aliveExternalName = null;
        foreach (var externalName in externalNames)
        {
            switch (externalName)
            {
                case ExternalNames.Futu:
                    if (!CheckFutuConnectivity())
                    {
                        MarkConnectivityFailure(ExternalNames.Futu);
                        continue;
                        // initialize Futu's QuotationService.
                    }
                    break;
            }
        }

        if (!aliveExternalName.IsBlank())
        {
            return await _quotationEngine.SubscribeAsync(security);
        }

        return new ExternalConnectionState
        {
            Action = ConnectionActionType.Subscribe,
            StatusCode = nameof(StatusCodes.NoAliveExternals),
            ExternalPartyId = security.Exchange,
            Description = "None of the external quotation data sources are alive.",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    private bool CheckFutuConnectivity()
    {
        throw new NotImplementedException();
    }

    private void MarkConnectivityFailure(string externalName)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> SubscribeTickAsync(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllOhlcsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllTicksAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOhlcAsync(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeTickAsync(Security security)
    {
        throw new NotImplementedException();
    }
}
