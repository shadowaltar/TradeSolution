using Common;
using log4net;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;

namespace TradeConnectivity.Binance.Services;
public class Quotation : IExternalQuotationManagement
{
    private static readonly ILog _log = Logger.New();
    private readonly ConcurrentDictionary<string, ClientWebSocket> _webSockets = new();
    private readonly Dictionary<(int, IntervalType), MessageBroker<OhlcPrice>> _messageBrokers = new();

    private readonly ConcurrentDictionary<int, HashSet<IntervalType>> _registeredIntervals = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;

    public string Name => ExternalNames.Binance;

    public event Action<int, OhlcPrice>? NextOhlc;
    public event Action<int, OrderBook>? NextOrderBook;

    public Quotation(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExternalConnectionState> Initialize()
    {
        return null;
    }

    public async Task<ExternalConnectionState> Disconnect()
    {
        foreach (var (name, ws) in _webSockets)
        {
            await CloseWebSocket(ws, name);
        }
        return null;
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType intervalType)
    {
        if (intervalType == IntervalType.Unknown)
        {
            intervalType = IntervalType.OneMinute;
        }

        var wsName = $"{security.Code.ToLowerInvariant()}@kline_{IntervalTypeConverter.ToIntervalString(intervalType).ToLowerInvariant()}";
        Uri uri = new($"{RootUrls.DefaultWs}/stream?streams={wsName}");

        ClientWebSocket ws = new();
        _webSockets[wsName] = ws;

        var intervals = _registeredIntervals.GetOrCreate(security.Id);
        intervals.Add(intervalType);

        await ws.ConnectAsync(uri, default);

        var broker = _messageBrokers.GetOrCreate((security.Id, intervalType));
        broker.NewItem += price => NextOhlc?.Invoke(security.Id, price);

        void OnReceivedString(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
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
                broker!.Enqueue(price);
            }
        }

        ws.Receive(OnReceivedString);

        return new ExternalConnectionState
        {
            Action = ExternalActionType.Subscribe,
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
    /// <param name="interval"></param>
    /// <returns></returns>
    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval = IntervalType.Unknown)
    {
        // single interval
        if (interval != IntervalType.Unknown)
        {
            return await InternalUnsubscribe(this, security, interval);
        }

        // all subscribed intervals
        var subStates = new List<ExternalConnectionState>();
        if (_registeredIntervals.TryGetValue(security.Id, out var intervals))
        {
            foreach (var i in intervals)
            {
                subStates.Add(await InternalUnsubscribe(this, security, i));
            }
        }
        return ExternalConnectionStates.UnsubscribedMultipleRealTimeOhlc(security, subStates);


        // unsubscribe the webSocket, and dispose the message broker
        static async Task<ExternalConnectionState> InternalUnsubscribe(Quotation quotation, Security security, IntervalType interval)
        {
            if (quotation._messageBrokers.TryGetValue((security.Id, interval), out var broker))
            {
                broker.Dispose();
            }
            var wsName = $"{security.Code}@kline_{IntervalTypeConverter.ToIntervalString(interval)}".ToLowerInvariant();
            if (quotation._webSockets.TryGetValue(wsName, out var ws))
            {
                var result = await CloseWebSocket(ws, wsName);
                quotation._webSockets.TryRemove(wsName, out _);
                if (result)
                    return ExternalConnectionStates.UnsubscribedRealTimeOhlcOk(security, interval);
            }
            return ExternalConnectionStates.UnsubscribedRealTimeOhlcFailed(security, interval);
        }
    }
    
    public async Task<ExternalConnectionState> UnsubscribeAllOhlc()
    {
        throw new NotImplementedException();
    }

    public async Task<OrderBook?> GetCurrentOrderBook(Security security)
    {
        if (!security.IsFrom(ExternalNames.Binance))
            return null;
        var url = $"{RootUrls.DefaultHttps}/depth?symbol={security.Code}";
        var json = await _httpClient.GetStringAsync(url);

        //        var json = @"
        //{""lastUpdateId"":38095967855,""bids"":[[""29281.44000000"",""15.72620000""],[""29281.30000000"",""0.04300000""],[""29281.28000000"",""0.20000000""],[""29281.25000000"",""0.62407000""],[""29281.24000000"",""2.38342000""],[""29281.22000000"",""0.14176000""],[""29281.05000000"",""0.06584000""],[""29281.04000000"",""0.01600000""],[""29281.01000000"",""0.00035000""],[""29280.85000000"",""2.00000000""],[""29280.68000000"",""1.04000000""],[""29280.43000000"",""0.00090000""],[""29280.42000000"",""0.00170000""],[""29280.35000000"",""0.00170000""],[""29280.19000000"",""0.34161000""],[""29280.15000000"",""0.20000000""],[""29280.02000000"",""0.00683000""],[""29280.01000000"",""0.12009000""],[""29280.00000000"",""0.15310000""],[""29279.98000000"",""0.06000000""],[""29279.59000000"",""0.20000000""],[""29279.56000000"",""0.20000000""],[""29279.55000000"",""0.00090000""],[""29279.54000000"",""0.00170000""],[""29279.47000000"",""0.00170000""],[""29279.40000000"",""0.06834000""],[""29279.27000000"",""0.01173000""],[""29279.22000000"",""0.00200000""],[""29278.74000000"",""0.00656000""],[""29278.67000000"",""0.00090000""],[""29278.66000000"",""0.00170000""],[""29278.65000000"",""0.20000000""],[""29278.59000000"",""0.00170000""],[""29278.52000000"",""0.23467000""],[""29278.51000000"",""0.39113000""],[""29278.34000000"",""0.00200000""],[""29278.33000000"",""0.01100000""],[""29278.14000000"",""0.10000000""],[""29278.13000000"",""0.07034000""],[""29278.05000000"",""0.16000000""],[""29278.00000000"",""0.15310000""],[""29277.99000000"",""0.20493000""],[""29277.97000000"",""0.23243000""],[""29277.84000000"",""0.20000000""],[""29277.79000000"",""0.00090000""],[""29277.78000000"",""0.00170000""],[""29277.75000000"",""0.00683000""],[""29277.71000000"",""0.20170000""],[""29277.59000000"",""0.34164000""],[""29277.42000000"",""0.02772000""],[""29277.39000000"",""0.07200000""],[""29277.14000000"",""0.01029000""],[""29277.08000000"",""0.29256000""],[""29276.91000000"",""0.00090000""],[""29276.90000000"",""0.00170000""],[""29276.89000000"",""0.51256000""],[""29276.87000000"",""0.20000000""],[""29276.83000000"",""0.00170000""],[""29276.77000000"",""0.20000000""],[""29276.59000000"",""0.48057000""],[""29276.58000000"",""0.16000000""],[""29276.55000000"",""0.20000000""],[""29276.53000000"",""0.17085000""],[""29276.51000000"",""0.00200000""],[""29276.31000000"",""0.00035000""],[""29276.30000000"",""0.05737000""],[""29276.20000000"",""0.00700000""],[""29276.17000000"",""0.00690000""],[""29276.13000000"",""0.10000000""],[""29276.12000000"",""0.20000000""],[""29276.11000000"",""0.07310000""],[""29276.03000000"",""0.00090000""],[""29276.02000000"",""0.00170000""],[""29276.00000000"",""0.15943000""],[""29275.95000000"",""0.00170000""],[""29275.83000000"",""0.20000000""],[""29275.81000000"",""0.12516000""],[""29275.69000000"",""0.34166000""],[""29275.63000000"",""0.07310000""],[""29275.57000000"",""0.00172000""],[""29275.42000000"",""0.32401000""],[""29275.41000000"",""0.01400000""],[""29275.23000000"",""0.20000000""],[""29275.15000000"",""0.00090000""],[""29275.14000000"",""0.00170000""],[""29275.13000000"",""1.23019000""],[""29275.12000000"",""0.09018000""],[""29275.10000000"",""0.44422000""],[""29275.07000000"",""0.00170000""],[""29274.99000000"",""0.32963000""],[""29274.89000000"",""0.20000000""],[""29274.83000000"",""0.00121000""],[""29274.77000000"",""0.00095000""],[""29274.76000000"",""0.00100000""],[""29274.64000000"",""2.05025000""],[""29274.41000000"",""0.16398000""],[""29274.40000000"",""0.20000000""],[""29274.36000000"",""0.30000000""],[""29274.27000000"",""0.00090000""],[""29274.26000000"",""0.00170000""]],""asks"":[[""29281.45000000"",""4.99231000""],[""29281.47000000"",""0.00200000""],[""29281.48000000"",""0.04852000""],[""29281.58000000"",""0.74843000""],[""29281.62000000"",""0.46201000""],[""29281.63000000"",""0.00750000""],[""29281.65000000"",""2.12419000""],[""29281.71000000"",""0.00161000""],[""29281.81000000"",""0.00083000""],[""29281.92000000"",""0.02406000""],[""29281.98000000"",""0.34159000""],[""29282.00000000"",""0.15310000""],[""29282.11000000"",""0.00170000""],[""29282.18000000"",""0.00170000""],[""29282.19000000"",""0.00090000""],[""29282.28000000"",""0.00038000""],[""29282.35000000"",""0.00355000""],[""29282.43000000"",""0.00038000""],[""29282.50000000"",""0.03415000""],[""29282.53000000"",""0.03416000""],[""29282.60000000"",""0.00036000""],[""29282.99000000"",""0.00170000""],[""29283.00000000"",""0.20000000""],[""29283.06000000"",""0.00292000""],[""29283.07000000"",""0.00619000""],[""29283.18000000"",""0.00526000""],[""29283.20000000"",""0.00070000""],[""29283.33000000"",""0.00053000""],[""29283.43000000"",""0.20000000""],[""29283.45000000"",""0.00200000""],[""29283.52000000"",""0.00039000""],[""29283.75000000"",""0.00074000""],[""29283.85000000"",""0.51269000""],[""29283.87000000"",""0.00170000""],[""29283.94000000"",""0.00208000""],[""29283.95000000"",""0.00445000""],[""29284.00000000"",""0.15310000""],[""29284.05000000"",""0.00096000""],[""29284.11000000"",""0.00040000""],[""29284.14000000"",""0.00535000""],[""29284.16000000"",""0.00407000""],[""29284.29000000"",""0.20000000""],[""29284.32000000"",""0.00066000""],[""29284.35000000"",""0.00203000""],[""29284.36000000"",""0.00075000""],[""29284.38000000"",""0.00122000""],[""29284.48000000"",""0.02693000""],[""29284.51000000"",""0.00035000""],[""29284.53000000"",""0.03415000""],[""29284.61000000"",""0.00038000""],[""29284.66000000"",""0.00037000""],[""29284.72000000"",""1.28472000""],[""29284.75000000"",""0.00170000""],[""29284.77000000"",""0.11823000""],[""29284.78000000"",""0.05862000""],[""29284.82000000"",""0.00170000""],[""29284.83000000"",""0.00090000""],[""29284.89000000"",""0.00036000""],[""29284.95000000"",""0.00200000""],[""29285.00000000"",""0.05115000""],[""29285.07000000"",""0.20000000""],[""29285.18000000"",""0.00254000""],[""29285.23000000"",""0.20000000""],[""29285.26000000"",""0.00036000""],[""29285.30000000"",""0.08542000""],[""29285.32000000"",""0.00097000""],[""29285.57000000"",""0.30315000""],[""29285.58000000"",""0.00050000""],[""29285.59000000"",""0.63709000""],[""29285.63000000"",""0.00170000""],[""29285.67000000"",""0.00045000""],[""29285.69000000"",""0.00035000""],[""29285.70000000"",""0.00292000""],[""29285.71000000"",""0.00202000""],[""29285.86000000"",""0.23906000""],[""29285.87000000"",""0.00845000""],[""29285.90000000"",""0.12532000""],[""29286.12000000"",""0.03415000""],[""29286.17000000"",""0.20000000""],[""29286.21000000"",""0.00038000""],[""29286.34000000"",""0.00200000""],[""29286.43000000"",""0.16000000""],[""29286.44000000"",""0.20000000""],[""29286.46000000"",""0.00046000""],[""29286.51000000"",""0.00170000""],[""29286.52000000"",""0.00084000""],[""29286.58000000"",""0.00170000""],[""29286.59000000"",""0.00090000""],[""29286.70000000"",""0.38834000""],[""29286.71000000"",""0.20000000""],[""29286.86000000"",""0.34153000""],[""29286.87000000"",""0.02212000""],[""29286.88000000"",""0.12692000""],[""29286.90000000"",""0.00041000""],[""29286.93000000"",""0.00068000""],[""29286.95000000"",""0.00055000""],[""29287.01000000"",""0.00122000""],[""29287.02000000"",""0.16000000""],[""29287.06000000"",""0.00049000""],[""29287.09000000"",""0.00046000""]]}
        //";

        // TODO make use of object pool
        var orderBook = new OrderBook();
        var o = JsonNode.Parse(json)?.AsObject();
        var bidArray = o!["bids"]!.AsArray();
        var askArray = o!["asks"]!.AsArray();
        foreach (var bid in bidArray!)
        {
            var depth = new OrderBookLevel
            {
                Price = bid![0]!.AsValue().GetDecimal(),
                Volume = bid![1]!.AsValue().GetDecimal()
            };
            orderBook.Bids.Add(depth);
        }
        foreach (var ask in askArray!)
        {
            var depth = new OrderBookLevel
            {
                Price = ask![0]!.AsValue().GetDecimal(),
                Volume = ask![1]!.AsValue().GetDecimal()
            };
            orderBook.Asks.Add(depth);
        }
        return orderBook;
    }

    /// <summary>
    /// Close a web socket.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ws"></param>
    /// <returns></returns>
    private static async Task<bool> CloseWebSocket(ClientWebSocket ws, string name)
    {
        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Closed", default);
            ws.Dispose();
            _log.Info($"Gracefully closed webSocket {name}.");
            return true;
        }
        catch (Exception e)
        {
            _log.Error($"Failed to close webSocket {name}.", e);
            try
            {
                // a final attempt to close the WebSocket
                ws.Dispose();
            }
            catch
            {
                _log.Error($"Failed to dispose webSocket {name} again.", e);
            }
            return false;
        }
    }

    private void Process()
    {

    }

    public Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType = IntervalType.Unknown)
    {
        throw new NotImplementedException();
    }
}
