using Common;
using log4net;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Services;
public class Quotation : IExternalQuotationManagement
{
    public string Name => ExternalNames.Binance;
    private static readonly ILog _log = Logger.New();
    private readonly ConcurrentDictionary<string, ClientWebSocket> _webSockets = new();
    private readonly Dictionary<(int, IntervalType), MessageBroker<OhlcPrice>> _messageBrokers = new();

    private readonly ConcurrentDictionary<int, HashSet<IntervalType>> _registeredIntervals = new();

    public event Action<int, OhlcPrice>? NewOhlc;

    public Quotation()
    {
    }

    public async Task<ExternalConnectionState> Initialize()
    {
        return null;
    }

    public async Task<ExternalConnectionState> Disconnect()
    {
        foreach (var (name, ws) in _webSockets)
        {
            await CloseWebSocket(name, ws);
        }
        return null;
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        if (intervalType == IntervalType.Unknown)
        {
            intervalType = IntervalType.OneMinute;
        }

        //Task.Factory.StartNew(() => Run(),
        //                      _cancellationTokenSource.Token,
        //                      TaskCreationOptions.LongRunning,
        //                      TaskScheduler.Default);

        var wsName = $"{security.Code.ToLowerInvariant()}@kline_{IntervalTypeConverter.ToIntervalString(intervalType).ToLowerInvariant()}";
        Uri uri = new($"wss://stream.binance.com:9443/stream?streams={wsName}");

        ClientWebSocket ws = new();
        _webSockets[wsName] = ws;

        var intervals = _registeredIntervals.GetOrCreate(security.Id);
        intervals.Add(intervalType);

        await ws.ConnectAsync(uri, default);

        var broker = _messageBrokers.GetOrCreate((security.Id, intervalType));
        broker.NewItem += OnNewItem;

        void OnNewItem(OhlcPrice price)
        {
            NewOhlc?.Invoke(security.Id, price);
        }

        // the thread is stopped 

        _ = Task.Factory.StartNew(async t =>
        {
            var buffer = new byte[1024];
            var bytes = new List<byte>();
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            while (!result.CloseStatus.HasValue)
            {
                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                bytes.AddRange(buffer.Take(result.Count));

                if (!result.EndOfMessage)
                    continue;

                string json = Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);
                var node = JsonNode.Parse(json);
                if (node != null)
                {
                    var dataNode = node.AsObject()["data"]!.AsObject();
                    //var asOfTime = DateUtils.FromUnixMs(dataNode["E"]!.GetValue<long>());
                    var kLineNode = dataNode["k"]!.AsObject();
                    var start = DateUtils.FromUnixMs(kLineNode["t"]!.GetValue<long>());
                    var o = kLineNode["o"]!.GetValue<string>().ParseDecimal();
                    var h = kLineNode["h"]!.GetValue<string>().ParseDecimal();
                    var l = kLineNode["l"]!.GetValue<string>().ParseDecimal();
                    var c = kLineNode["c"]!.GetValue<string>().ParseDecimal();
                    var v = kLineNode["v"]!.GetValue<string>().ParseDecimal();
                    var price = new OhlcPrice(o, h, l, c, v, start);
                    broker.Enqueue(price);
                }
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
        }, TaskCreationOptions.LongRunning, CancellationToken.None);

        return new ExternalConnectionState
        {
            Action = ConnectionActionType.Subscribe,
            StatusCode = nameof(StatusCodes.SubscriptionOk),
            ExternalPartyId = security.Exchange,
            Description = "Subscribed",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    /// <summary>
    /// Unsubscribe OHLC data stream.
    /// If no interval type specified, will unsubscribe all of the specific security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="intervalType"></param>
    /// <returns></returns>
    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        var code = security.Code.ToLowerInvariant();
        if (intervalType != IntervalType.Unknown)
        {
            await Unsubscribe(security.Id, code, intervalType);
        }
        else
        {
            if (_registeredIntervals.TryGetValue(security.Id, out var intervals))
            {
                foreach (var interval in intervals)
                {
                    await Unsubscribe(security.Id, code, intervalType);
                }
            }
        }

        // unsubscribe the webSocket, and dispose the message broker
        async Task Unsubscribe(int id, string code, IntervalType interval)
        {
            if (_messageBrokers.TryGetValue((id, interval), out var broker))
            {
                broker.Dispose();
            }
            var wsName = $"{code}@kline_{IntervalTypeConverter.ToIntervalString(interval).ToLowerInvariant()}";
            if (_webSockets.TryGetValue(wsName, out var ws))
            {
                await CloseWebSocket(wsName, ws);
                _webSockets.TryRemove(wsName, out _);
            }
        }

        return null;
    }

    private static async Task CloseWebSocket(string name, ClientWebSocket ws)
    {
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Closed", default);
        ws.Dispose();
        _log.Info($"Gracefully closed websocket {name}.");
    }

    private void Process()
    {

    }

    ExternalConnectionState IExternalQuotationManagement.SubscribeOhlc(Security security, IntervalType intervalType)
    {
        throw new NotImplementedException();
    }
}
