using Common;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeDataCore.MarketData;
public class RealTimeMarketDataService : IMarketDataService, IDisposable
{
    private readonly IExternalQuotationManagement _external;

    public IExternalQuotationManagement External => _external;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, Tick> NextTick;

    public RealTimeMarketDataService(IExternalQuotationManagement external)
    {
        _external = external;
        _external.NextOhlc += OnNextOhlc;
    }

    private void OnNextOhlc(int securityId, OhlcPrice price)
    {
        NextOhlc?.Invoke(securityId, price);
    }

    public async Task Initialize()
    {
        await _external.Initialize();
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security)
    {
        var externalNames = MarketDataSources.GetExternalNames(security);
        if (externalNames.IsNullOrEmpty())
        {
            return new ExternalConnectionState
            {
                Action = ExternalActionType.Subscribe,
                StatusCode = nameof(StatusCodes.InvalidArgument),
                ExternalPartyId = security.Exchange,
                Description = "Unknown combination of security type, sub-type and exchange name: " + security,
                Type = SubscriptionType.RealTimeMarketData,
                UniqueConnectionId = "",
            };
        }

        return await _external.SubscribeOhlc(security);
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
        return await _external.UnsubscribeOhlc(security);
    }

    public Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _external.NextOhlc -= OnNextOhlc;
        NextOhlc = null;
    }
}
