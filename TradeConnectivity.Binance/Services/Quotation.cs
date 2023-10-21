using Common;
using Common.Web;
using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeConnectivity.Binance.Utils;

namespace TradeConnectivity.Binance.Services;
public class Quotation : IExternalQuotationManagement
{
    private static readonly ILog _log = Logger.New();
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly RequestBuilder _requestBuilder;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ExtendedWebSocket> _webSockets = new();

    private readonly Dictionary<(int, IntervalType), MessageBroker<OhlcPrice>> _ohlcPriceBrokers = new();
    private readonly Dictionary<(int, IntervalType), OhlcPrice> _lastOhlcPrices = new();
    private readonly Dictionary<int, HashSet<IntervalType>> _registeredIntervals = new();

    private readonly Dictionary<int, MessageBroker<ExtendedTick>> _tickBrokers = new();
    private readonly Pool<ExtendedTick> _tickPool = new();
    private readonly Dictionary<int, ExtendedTick> _lastTicks = new();

    public string Name => ExternalNames.Binance;

    public event Action<int, OhlcPrice, bool>? NextOhlc;
    public event Action<int, OrderBook>? NextOrderBook;
    public event Action<ExtendedTick>? NextTick;

    public Quotation(IExternalConnectivityManagement connectivity,
                     HttpClient httpClient,
                     ApplicationContext context,
                     KeyManager keyManager)
    {
        _httpClient = context.IsExternalProhibited ? new FakeHttpClient() : httpClient;
        _connectivity = connectivity;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
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
        return ExternalConnectionStates.UnsubscribedAll();
    }

    public async Task<ExternalQueryState> GetPrices(params string[] symbols)
    {
        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/ticker/price";
        List<(string, string)>? parameters = symbols.IsNullOrEmpty() ? null : new(1);

        if (symbols.Length == 1)
            parameters!.Add(("symbol", symbols[0]));
        else
            parameters!.Add(("symbols", Uri.EscapeDataString(Json.Serialize(symbols))));

        using var request = _requestBuilder.Build(HttpMethod.Get, url, parameters);
        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        // example:
        //        var json = @"
        //[
        //    {
        //        ""symbol"": ""BTCUSDT"",
        //        ""price"": ""29783.88000000""
        //    },
        //    {
        //        ""symbol"": ""BNBUSDT"",
        //        ""price"": ""213.20000000""
        //    }
        //]";
        var prices = new Dictionary<string, decimal>();
        string content = "";
        string errorMessage = "";
        if (symbols.Length == 1)
        {
            if (!response.ParseJsonObject(out content, out var json, out errorMessage, _log))
            {
                return GetErrorState();
            }

            var (s, p) = ParsePrice(json);
            prices[s] = p;
        }
        else
        {
            if (!response.ParseJsonArray(out content, out var json, out errorMessage, _log))
            {
                return GetErrorState();
            }

            for (int i = 0; i < json.Count; i++)
            {
                JsonNode? node = json[i];
                var obj = node?.AsObject();
                if (obj == null)
                    continue;

                var (s, p) = ParsePrice(obj);
                prices[s] = p;
            }
        }

        return ExternalQueryStates.QueryPrices(prices, content, connId, true).RecordTimes(rtt, swOuter);

        static (string symbol, decimal price) ParsePrice(JsonObject json)
        {
            return (json.GetString("symbol"), json.GetDecimal("price"));
        }

        ExternalQueryState GetErrorState()
        {
            return ExternalQueryStates.QueryPrices(prices, content, connId, false, errorMessage, Errors.ProcessErrorMessage(errorMessage));
        }
    }

    public async Task<ExternalQueryState> GetAccount()
    {
        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/account";
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var json, out var errorMessage, _log))
        {
            var subCode = Errors.ProcessErrorMessage(errorMessage);
            return ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, subCode, content, connId, errorMessage);
        }
        // example json: responseJson = @"{ ""makerCommission"": 0, ""takerCommission"": 0, ""buyerCommission"": 0, ""sellerCommission"": 0, ""commissionRates"": { ""maker"": ""0.00000000"", ""taker"": ""0.00000000"", ""buyer"": ""0.00000000"", ""seller"": ""0.00000000"" }, ""canTrade"": true, ""canWithdraw"": false, ""canDeposit"": false, ""brokered"": false, ""requireSelfTradePrevention"": false, ""preventSor"": false, ""updateTime"": 1690995029309, ""accountType"": ""SPOT"", ""assets"": [ { ""asset"": ""BNB"", ""free"": ""1000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BTC"", ""free"": ""1.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BUSD"", ""free"": ""10000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""ETH"", ""free"": ""100.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""LTC"", ""free"": ""500.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""TRX"", ""free"": ""500000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""USDT"", ""free"": ""8400.00000000"", ""locked"": ""1600.00000000"" }, { ""asset"": ""XRP"", ""free"": ""50000.00000000"", ""locked"": ""0.00000000"" } ], ""permissions"": [ ""SPOT"" ], ""uid"": 1688996631782681271 }";
        var account = new Account
        {
            Type = json.GetString("accountType"),
            ExternalAccount = json.GetLong("uid").ToString(),
            UpdateTime = json.GetLong("updateTime").FromUnixMs(),
        };
        return ExternalQueryStates.QueryAccount(content, connId, account).RecordTimes(rtt, swOuter);
    }

    public async Task<ExternalConnectionState> SubscribeOhlc(Security security, IntervalType interval)
    {
        if (interval == IntervalType.Unknown)
            interval = IntervalType.OneMinute;

        var wsName = $"{nameof(OhlcPrice)}_{security.Id}_{interval}";
        var streamName = $"{security.Code.ToLowerInvariant()}@kline_{IntervalTypeConverter.ToIntervalString(interval).ToLowerInvariant()}";
        Uri uri = new($"{_connectivity.RootWebSocketUrl}/stream?streams={streamName}");

        lock (_registeredIntervals)
            _registeredIntervals.GetOrCreate(security.Id).Add(interval);

        _lastOhlcPrices[(security.Id, interval)] = new OhlcPrice(); // pre-create to avoid threading issue

        bool isComplete = false;
        var broker = _ohlcPriceBrokers.GetOrCreate((security.Id, interval),
            () => new MessageBroker<OhlcPrice>(security.Id),
            (k, v) => v.Run());
        broker.NewItem += price => NextOhlc?.Invoke(security.Id, price, isComplete); // broker.Dispose() will clear this up if needed

        string message = "";
        var webSocket = new ExtendedWebSocket(_log);
        webSocket.Listen(uri, OnReceivedString, OnWebSocketCreated);
        Threads.WaitUntil(() => _webSockets.ThreadSafeContains(wsName));
        return ExternalConnectionStates.Subscribed(SubscriptionType.RealTimeMarketData, message);


        void OnWebSocketCreated()
        {
            message = $"Subscribed to OHLC price for {security.Code} on {security.Exchange} every {interval}";
            _log.Info(message);
            _webSockets.ThreadSafeSet(wsName, webSocket);
        }

        void OnReceivedString(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            var node = JsonNode.Parse(json);
            if (node != null)
            {
                //var example = @"{""stream"":""btctusd@kline_1m"",""data"":{""e"":""kline"",""E"":1693333305611,""s"":""BTCTUSD"",""k"":{""t"":1693333260000,""T"":1693333319999,""s"":""BTCTUSD"",""i"":""1m"",""f"":349256438,""L"":349258039,""o"":""27951.39000000"",""c"":""27935.49000000"",""h"":""27952.00000000"",""l"":""27923.03000000"",""v"":""57.60304000"",""n"":1602,""x"":false,""q"":""1609180.77265510"",""V"":""24.67172000"",""Q"":""689202.76346730"",""B"":""0""}}}";
                var dataNode = node.AsObject()["data"]!.AsObject();
                var price = _lastOhlcPrices[(security.Id, interval)];

                //var asOfTime = DateUtils.FromUnixMs(dataNode["E"]!.GetValue<long>());
                var kLineNode = dataNode["k"]!.AsObject();
                var start = DateUtils.FromUnixMs(kLineNode["t"]!.GetValue<long>());

                isComplete = kLineNode.GetBoolean("x");

                var o = kLineNode.GetDecimal("o");
                var h = kLineNode.GetDecimal("h");
                var l = kLineNode.GetDecimal("l");
                var c = kLineNode.GetDecimal("c");
                var v = kLineNode.GetDecimal("v");
                if (price == null)
                {
                    price = new OhlcPrice(o, h, l, c, v, start);
                }
                else
                {
                    price.O = o;
                    price.H = h;
                    price.L = l;
                    price.C = c;
                    price.V = v;
                    price.T = start;
                }
                _lastOhlcPrices[(security.Id, interval)] = price;
                broker!.Enqueue(price);
            }
        }
    }

    /// <summary>
    /// Unsubscribe OHLC data stream.
    /// If no interval type specified, will unsubscribe all of the specific security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval)
    {
        if (!security.IsFrom(ExternalNames.Binance))
            return ExternalConnectionStates.InvalidSecurity(SubscriptionType.TickData, ActionType.Unsubscribe);

        var broker = _ohlcPriceBrokers.ThreadSafeGetAndRemove((security.Id, interval));
        broker?.Dispose();
        var wsName = $"{nameof(OhlcPrice)}_{security.Id}_{interval}";
        var ws = _webSockets.ThreadSafeGetAndRemove(wsName);
        if (ws != null && await CloseWebSocket(ws, wsName))
        {
            return ExternalConnectionStates.UnsubscribedRealTimeOhlcOk(security, interval);
        }
        return ExternalConnectionStates.UnsubscribedRealTimeOhlcFailed(security, interval);
    }

    public async Task<ExternalConnectionState> UnsubscribeAllOhlc()
    {
        throw new NotImplementedException();
    }

    public async Task<OrderBook?> GetCurrentOrderBook(Security security)
    {
        if (!security.IsFrom(ExternalNames.Binance))
            return null;
        var url = $"{_connectivity.RootUrl}/depth?symbol={security.Code}";
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

    public async Task<ExternalConnectionState> SubscribeTick(Security security)
    {
        if (!security.IsFrom(ExternalNames.Binance))
            return ExternalConnectionStates.InvalidSecurity(SubscriptionType.TickData, ActionType.Subscribe);

        var wsName = $"{nameof(ExtendedTick)}_{security.Id}";
        var streamName = $"{security.Code.ToLowerInvariant()}@bookTicker";
        Uri uri = new($"{_connectivity.RootWebSocketUrl}/stream?streams={streamName}");

        _lastTicks[security.Id] = new ExtendedTick { SecurityId = security.Id };
        var broker = _tickBrokers.GetOrCreate(security.Id, () => new MessageBroker<ExtendedTick>(security.Id), (k, v) => v.Run());
        broker.NewItem += OnNextTick; // broker.Dispose() will clear this up if needed

        string message = "";
        var webSocket = new ExtendedWebSocket(_log);
        webSocket.Listen(uri, OnReceivedString, OnWebSocketCreated);
        Threads.WaitUntil(() => _webSockets.ThreadSafeContains(wsName));
        return ExternalConnectionStates.Subscribed(SubscriptionType.TickData, message);


        void OnWebSocketCreated()
        {
            message = $"Subscribed to real time tick data for {security.Code} on {security.Exchange}";
            _log.Info(message);
            _webSockets.ThreadSafeSet(wsName, webSocket);
        };

        void OnReceivedString(byte[] bytes)
        {
            var now = DateTime.UtcNow;
            string json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            var node = JsonNode.Parse(json)?.AsObject()["data"]?.AsObject();
            if (node != null)
            {
                //var example = @"{
                //  ""u"":400900217,     // order book updateId
                //  ""s"":""BNBUSDT"",     // symbol
                //  ""b"":""25.35190000"", // best bid price
                //  ""B"":""31.21000000"", // best bid qty
                //  ""a"":""25.36520000"", // best ask price
                //  ""A"":""40.66000000""  // best ask qty
                //}";
                var tick = _tickPool.Lease();
                var s = node.GetString("s"); // symbol
                var b = node.GetDecimal("b");
                var bq = node.GetDecimal("B");
                var a = node.GetDecimal("a");
                var aq = node.GetDecimal("A");
                tick.Bid = b;
                tick.BidSize = bq;
                tick.Ask = a;
                tick.AskSize = aq;
                tick.SecurityCode = s;
                tick.SecurityId = security.Id;
                tick.Time = now;
                broker!.Enqueue(tick);
            }
        }
    }

    private void OnNextTick(ExtendedTick tick)
    {
        _log.Info(tick);
        NextTick?.Invoke(tick);
        _tickPool.Return(tick);
    }

    public async Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        if (!security.IsFrom(ExternalNames.Binance))
            return ExternalConnectionStates.InvalidSecurity(SubscriptionType.TickData, ActionType.Unsubscribe);

        // do not clear the lastTick cache, only the broker cache
        var broker = _tickBrokers.ThreadSafeGetAndRemove(security.Id);
        broker?.Dispose();
        var wsName = $"{nameof(ExtendedTick)}_{security.Id}";
        var ws = _webSockets.ThreadSafeGetAndRemove(wsName);
        if (ws != null && await CloseWebSocket(ws, wsName))
        {
            return ExternalConnectionStates.UnsubscribedTickOk(security);
        }
        return ExternalConnectionStates.UnsubscribedTickFailed(security);
    }

    public Task<ExternalConnectionState> SubscribeOrderBook(Security security, IntervalType intervalType)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeOrderBook(Security security, IntervalType intervalType)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Close a web socket.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ws"></param>
    /// <returns></returns>
    private static async Task<bool> CloseWebSocket(ExtendedWebSocket ws, string name)
    {
        try
        {
            await ws.Close();
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
}
