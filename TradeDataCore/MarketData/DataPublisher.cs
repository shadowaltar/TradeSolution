using Common;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.MarketData;
public class DataPublisher
{
    private readonly IMarketDataService _marketDataService;
    private readonly Dictionary<(int, IntervalType), BlockingCollection<OhlcPrice>> _ohlcQueues = new();
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

    private void OnOhlcPriceReceived(int securityId, OhlcPrice price, IntervalType interval, bool isComplete)
    {
        var queue = _ohlcQueues.ThreadSafeGetOrCreate((securityId, interval));
    }

    public async Task PublishOhlc(WebSocket webSocket, Security security, IntervalType interval)
    {
        var queue = _ohlcQueues.ThreadSafeGetOrCreate((security.Id, interval));
        
        var receiveBuffer = new ArraySegment<byte>(new Byte[8192]);

        using var ms = new MemoryStream();

        WebSocketReceiveResult? receiveResult = null;
        while (receiveResult == null || !receiveResult.CloseStatus.HasValue)
        {
            if (webSocket.State != WebSocketState.Open)
                continue;

            var item = queue.Take();

            var bytes = Encoding.UTF8.GetBytes(Json.Serialize(item));
            var outwardBytes = new ArraySegment<byte>(bytes);
            await webSocket.SendAsync(outwardBytes, WebSocketMessageType.Text, true, CancellationToken.None);

            do
            {
                receiveResult = await webSocket.ReceiveAsync(receiveBuffer, CancellationToken.None);
                ms.Write(receiveBuffer.Array, receiveBuffer.Offset, receiveBuffer.Count);
            }
            while (!receiveResult.EndOfMessage);

            switch(receiveResult.MessageType)
            {
                case WebSocketMessageType.Text:
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    break;
                case WebSocketMessageType.Binary:
                    break;
                case WebSocketMessageType.Close:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        await webSocket.CloseAsync(receiveResult.CloseStatus.Value,
                                   receiveResult.CloseStatusDescription,
                                   CancellationToken.None);
    }
}


