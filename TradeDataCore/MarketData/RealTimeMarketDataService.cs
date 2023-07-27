using Common;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IRealTimeMarketDataService, IDisposable
{
    private readonly IExternalQuotationManagement _quotation;

    public event Action<int, OhlcPrice>? NewOhlc;

    public RealTimeMarketDataService(IExternalQuotationManagement quotation)
    {
        _quotation = quotation;
        _quotation.NewOhlc += OnNewOhlc;
    }

    private void OnNewOhlc(int securityId, OhlcPrice price)
    {
        NewOhlc?.Invoke(securityId, price);
    }

    public async Task Initialize()
    {
        await _quotation.Initialize();
    }

    public ExternalConnectionState SubscribeOhlc(Security security)
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

        return _quotation.SubscribeOhlc(security);
    }

    public Task<ExternalConnectionState> SubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllOhlcs()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAllTicks()
    {
        throw new NotImplementedException();
    }

    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security)
    {
        return await _quotation.UnsubscribeOhlc(security);
    }

    public Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _quotation.NewOhlc -= OnNewOhlc;
        NewOhlc = null;
    }
}
