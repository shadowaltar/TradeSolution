using Common;
using log4net;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.CryptoSimulator.Services;
public class Quotation : IExternalQuotationManagement
{
    private static readonly ILog _log = Logger.New();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ClientWebSocket> _webSockets = new();
    private readonly Dictionary<(int, IntervalType), MessageBroker<OhlcPrice>> _messageBrokers = [];
    private readonly Dictionary<(int, IntervalType), OhlcPrice> _lastOhlcPrices = [];
    private readonly ConcurrentDictionary<int, HashSet<IntervalType>> _registeredIntervals = new();

    public string Name => ExternalNames.Binance;

    public double LatencyOneSide => throw new NotImplementedException();

    public double LatencyRoundTrip => throw new NotImplementedException();

    public TimeSpan LatencyRoundTripInTimeSpan => throw new NotImplementedException();

    public TimeSpan LatencyOneSideInTimeSpan => throw new NotImplementedException();

    public event Action<ExtendedTick>? NextTick;
    public event OhlcPriceReceivedCallback? NextOhlc;
    public event Action<ExtendedOrderBook>? NextOrderBook;

    public Quotation(IExternalConnectivityManagement connectivity, HttpClient httpClient)
    {
        _connectivity = connectivity;
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

    /// <summary>
    /// Unsubscribe OHLC data stream.
    /// If no interval type specified, will unsubscribe all of the specific security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public async Task<ExternalConnectionState> UnsubscribeOhlc(Security security, IntervalType interval = IntervalType.Unknown)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public async Task<ExternalConnectionState> UnsubscribeAllOhlc()
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public async Task<OrderBook?> GetCurrentOrderBook(Security security)
    {
        var orderBook = new OrderBook();
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

    public async Task<ExternalQueryState> GetPrices(params string[] symbols)
    {
        return ExternalQueryStates.Simulation<List<OhlcPrice>>([]);
    }

    public async Task<ExternalQueryState> GetPrice(string symbol)
    {
        return ExternalQueryStates.Simulation<List<OhlcPrice>>([]);
    }

    public ExternalConnectionState SubscribeTick(Security security)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public ExternalConnectionState SubscribeOrderBook(Security security, int? level = null)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public ExternalConnectionState SubscribeOhlc(Security security, IntervalType intervalType)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public async Task<ExternalConnectionState> UnsubscribeTick(Security security)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }

    public async Task<ExternalConnectionState> UnsubscribeOrderBook(Security security)
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.MarketData);
    }
}
