using Common;
using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeConnectivity.CryptoSimulator.Utils;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.CryptoSimulator.Services;

/// <summary>
/// Simulator execution logic.
/// </summary>
public class Execution : IExternalExecutionManagement
{
    private static readonly ILog _log = Logger.New();
    private readonly int _brokerId = ExternalNames.GetBrokerId(BrokerType.Binance);
    private readonly int _exchangeId = ExternalNames.GetExchangeId(ExchangeType.Binance);
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly HttpClient _httpClient;
    private readonly RequestBuilder _requestBuilder;
    private readonly IdGenerator _cancelIdGenerator;
    private readonly IdGenerator _tradeIdGenerator;
    private readonly ConcurrentDictionary<string, ClientWebSocket> _webSockets = new();

    private string? _listenKey;
    private Timer? _listenKeyTimer;

    public bool IsFakeOrderSupported => true;

    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCancelledCallback? OrderCancelled;
    public event AllOrderCancelledCallback? AllOrderCancelled;
    public event OrderReceivedCallback? OrderReceived;
    public event TradeReceivedCallback? TradeReceived;
    public event TradesReceivedCallback? TradesReceived;

    public event TransferredCallback? Transferred;
    public event AssetsChangedCallback? AssetsChanged;

    public Execution(IExternalConnectivityManagement connectivity,
                     HttpClient httpClient,
                     KeyManager keyManager)
    {
        _connectivity = connectivity;
        _httpClient = httpClient;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
        _cancelIdGenerator = new IdGenerator("CancelOrderIdGen");
        _tradeIdGenerator = new IdGenerator("TradeIdGen");
    }

    /// <summary>
    /// Send an order to Binance [SIGNED].
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> SendOrder(Order order)
    {
        var url = $"{_connectivity.RootUrl}/api/v3/order";
        return await SendOrder(url, order);
    }

    private async Task<ExternalQueryState> SendOrder(string url, Order order)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.SendOrder);
    }

    /// <summary>
    /// Cancel an order [SIGNED].
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> CancelOrder(Order order)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.CancelOrder);
    }

    /// <summary>
    /// Cancel all orders related to a security [SIGNED].
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<ExternalQueryState> CancelAllOrders(Security security)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.CancelOrder);
    }

    /// <summary>
    /// Get recent trades in the market [NONE].
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetMarketTrades(Security security)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetTrade);
    }

    /// <summary>
    /// Get an open or historical order by order id [SIGNED].
    /// Either <paramref name="orderId"/> or <paramref name="externalOrderId"/> must be provided.
    /// </summary>
    /// <param name="orderId">Our order id, aka Binance's client order id.</param>
    /// <param name="externalOrderId">Binance's order id, aka our external order id.</param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOrder(Security security, long orderId = 0, long externalOrderId = 0)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetOrder);
    }

    /// <summary>
    /// Get all open orders [SIGNED].
    /// If a <paramref name="security"/> is provided, only the open orders related to this security will be returned.
    /// Notice that there are no security ids inside orders if no security is provided.
    /// </summary>
    /// <param name="orderId">Our order id, aka Binance's client order id.</param>
    /// <param name="externalOrderId">Binance's order id, aka our external order id.</param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOpenOrders(Security? security = null)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetOrder);
    }

    /// <summary>
    /// Get orders given a security and time period [SIGNED].
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOrderHistory(Security security, DateTime start, DateTime end)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetOrder);
    }

    /// <summary>
    /// Update an order [SIGNED].
    /// Binance only supports cancel + replace order action.
    /// </summary>
    /// <param name="updatedOrder"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<ExternalQueryState> UpdateOrder(Order updatedOrder)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.UpdateOrder);
    }

    /// <summary>
    /// Get the speed limit for sending orders [SIGNED]
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOrderSpeedLimit()
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetFrequencyRestriction);
    }

    /// <summary>
    /// Get trades for a specific symbol [SIGNED].
    /// Valid combination with precedence:
    /// 1. symbol
    /// 2. symbol + orderId
    /// 3. symbol + start (limit to 500 entries)
    /// 4. symbol + start + end (limit to 500 entries)
    /// </summary>
    /// <param name="security"></param>
    /// <param name="orderId"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<ExternalQueryState> GetTrades(Security security,
                                                    long orderId = long.MinValue,
                                                    DateTime? start = null,
                                                    DateTime? end = null)
    {
        return ExternalQueryStates.SimulationOfError(ActionType.GetTrade);
    }

    /// <summary>
    /// Subscribe to current user data steam, including updates of account, balance, order and trade. [USER-STREAM]
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalConnectionState> Subscribe()
    {
        return ExternalConnectionStates.Simulation(SubscriptionType.RealTimeExecutionData);
    }

    public async Task<ExternalQueryState> Unsubscribe()
    {
        return ExternalQueryStates.Simulation<ActionType>(ActionType.Unsubscribe);
    }

    public async Task<ExternalQueryState> GetOrders(Security security, long oldestExternalOrderId = long.MinValue, DateTime? start = null, DateTime? end = null)
    {
        return ExternalQueryStates.Simulation<Order>(null);
    }

    public async Task<ExternalQueryState> GetAssetPositions(string accountId)
    {
        return ExternalQueryStates.Simulation<Asset>(null);
    }
}
