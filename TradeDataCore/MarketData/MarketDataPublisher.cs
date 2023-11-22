using System.Net.WebSockets;
using System.Text;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public class MarketDataPublisher
{
    private readonly IMarketDataService _marketDataService;

    public MarketDataPublisher(IMarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    public void Initialize()
    {
        _marketDataService.NextOhlc -= OnOhlcPriceReceived;
        _marketDataService.NextOhlc += OnOhlcPriceReceived;
        _marketDataService.NextOrderBook -= OnOrderBookReceived;
        _marketDataService.NextOrderBook += OnOrderBookReceived;
        _marketDataService.NextTick -= OnTickReceived;
        _marketDataService.NextTick += OnTickReceived;
    }

    private void OnTickReceived(int securityId, string securityCode, Tick tick)
    {
    }

    private void OnOrderBookReceived(ExtendedOrderBook orderBook)
    {
    }

    private void OnOhlcPriceReceived(int securityId, OhlcPrice price, bool isComplete)
    {
    }

    public async Task Process(string path, WebSocket webSocket)
    {
        while (true)
        {
            var bytes = Encoding.UTF8.GetBytes
        }
    }
}
