using System.Net.WebSockets;
using System.Text;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public class DataPublisher
{
    private readonly IMarketDataService _marketDataService;

    public DataPublisher(IMarketDataService marketDataService)
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

    public void Reset()
    {
        _marketDataService.NextOhlc -= OnOhlcPriceReceived;
        _marketDataService.NextOrderBook -= OnOrderBookReceived;
        _marketDataService.NextTick -= OnTickReceived;
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

    public async Task PublishOhlc(WebSocket webSocket, Security? security, IntervalType interval)
    {
        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            var message = "Hello World!";
            var bytes = Encoding.UTF8.GetBytes(message);
            var outwardBytes = new ArraySegment<byte>(buffer, 0, receiveResult.Count);
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(outwardBytes,
                                          receiveResult.MessageType,
                                          receiveResult.EndOfMessage,
                                          CancellationToken.None);
                var inwardBytes = new ArraySegment<byte>(buffer);
                receiveResult = await webSocket.ReceiveAsync(inwardBytes, CancellationToken.None);
            }
        }

        await webSocket.CloseAsync(receiveResult.CloseStatus.Value,
                                   receiveResult.CloseStatusDescription,
                                   CancellationToken.None);
    }
}
