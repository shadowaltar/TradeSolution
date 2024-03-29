﻿using Common;
using Common.Web;
using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeConnectivity.Binance.Utils;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.Binance.Services;

/// <summary>
/// Binance execution logic.
/// SIGNED mark: must provide both API Key and Signature (Secret Key);
/// in Binance doc they are also marked as TRADE / USER_DATA / MARGIN.
/// </summary>
public class Execution : IExternalExecutionManagement
{
    private static readonly ILog _log = Logger.New();
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly ApplicationContext _context;
    private readonly HttpClient _httpClient;
    private readonly RequestBuilder _requestBuilder;
    private readonly IdGenerator _cancelIdGenerator;
    private readonly IdGenerator _tradeIdGenerator;
    private readonly ConcurrentDictionary<string, ExtendedWebSocket> _webSockets = new();

    private readonly HashSet<long> _processedExternalTradeIds = new();

    private readonly MessageBroker<EventInvokerTask> _broker = new();

    private static readonly Dictionary<OrderType, string> _orderTypeToParameters = new()
    {
        { OrderType.Limit, "LIMIT" }, { OrderType.Market, "MARKET" },
        { OrderType.Stop, "STOP_LOSS" }, { OrderType.StopLimit, "STOP_LOSS_LIMIT" },
        { OrderType.TakeProfit, "TAKE_PROFIT" }, { OrderType.TakeProfitLimit, "TAKE_PROFIT_LIMIT" },
    };

    private string? _listenKey;
    private Timer? _listenKeyTimer;

    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCancelledCallback? OrderCancelled;
    public event AllOrderCancelledCallback? AllOrderCancelled;
    public event OrderReceivedCallback? OrderReceived;
    public event TradeReceivedCallback? TradeReceived;

    public event AssetsChangedCallback? AssetsChanged;
    public event TransferredCallback? Transferred;

    public Execution(IExternalConnectivityManagement connectivity,
                     HttpClient httpClient,
                     ApplicationContext context,
                     KeyManager keyManager)
    {
        _httpClient = context.IsExternalProhibited ? new FakeHttpClient() : httpClient;

        _connectivity = connectivity;
        _context = context;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
        _broker.NewItem += a => a.Run();
        _broker.Run();
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
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        var url = $"{_connectivity.RootUrl}/api/v3/order";
        var isOk = false;
        var swTotal = Stopwatch.StartNew();
        var parameters = new List<(string, string)>(16)
        {
            ("symbol", order.SecurityCode),
            ("side", order.Side.ToString().ToUpperInvariant()),
            ("type", _orderTypeToParameters[order.Type]),
            ("quantity", order.Quantity.ToString()),
            ("newClientOrderId", order.Id.ToString()),
        };
        if (order.Type is OrderType.Limit)
        {
            parameters.Add(("price", order.LimitPrice.ToString()));
            parameters.Add(("timeInForce", order.TimeInForce.ConvertEnumToDescription()));
        }
        else if (order.Type is OrderType.StopLimit or OrderType.TakeProfitLimit)
        {
            parameters.Add(("price", order.LimitPrice.ToString()));
            parameters.Add(("stopPrice", order.StopPrice.ToString()));
            parameters.Add(("timeInForce", order.TimeInForce.ConvertEnumToDescription()));
        }
        else if (order.Type is OrderType.Stop or OrderType.TakeProfit)
        {
            parameters.Add(("stopPrice", order.StopPrice.ToString()));
        }
        using var request = _requestBuilder.BuildSigned(HttpMethod.Post, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var jsonNode, out var errorMessage, _log))
        {
            order.Status = OrderStatus.UnknownResponse;
            return ExternalQueryStates.Error(ActionType.SendOrder, ResultCode.SendOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);
        }
        isOk = true;
        // example JSON: var content = @"{ ""symbol"": ""BTCUSDT"", ""externalOrderId"": 28, ""orderListId"": -1, ""clientOrderId"": ""6gCrw2kRUAF9CvJDGP16IP"", ""transactTime"": 1507725176595, ""price"": ""0.00000000"", ""origQty"": ""10.00000000"", ""executedQty"": ""10.00000000"", ""cummulativeQuoteQty"": ""10.00000000"", ""status"": ""FILLED"", ""timeInForce"": ""GTC"", ""type"": ""MARKET"", ""side"": ""SELL"", ""workingTime"": 1507725176595, ""selfTradePreventionMode"": ""NONE"", ""fills"": [ { ""price"": ""4000.00000000"", ""qty"": ""1.00000000"", ""commission"": ""4.00000000"", ""commissionAsset"": ""USDT"", ""externalTradeId"": 56 }, { ""price"": ""3999.00000000"", ""qty"": ""5.00000000"", ""commission"": ""19.99500000"", ""commissionAsset"": ""USDT"", ""externalTradeId"": 57 }, { ""price"": ""3998.00000000"", ""qty"": ""2.00000000"", ""commission"": ""7.99600000"", ""commissionAsset"": ""USDT"", ""externalTradeId"": 58 }, { ""price"": ""3997.00000000"", ""qty"": ""1.00000000"", ""commission"": ""3.99700000"", ""commissionAsset"": ""USDT"", ""externalTradeId"": 59 }, { ""price"": ""3995.00000000"", ""qty"": ""1.00000000"", ""commission"": ""3.99500000"", ""commissionAsset"": ""USDT"", ""externalTradeId"": 60 } ] }"
        var trades = new List<Trade>();

        var receivedOrder = new Order
        {
            Id = order.Id,
            Action = order.Action,
            AccountId = _context.AccountId,
            CreateTime = order.CreateTime,

            Type = order.Type,
            Side = order.Side,
            Price = 0, // should be zero when the order is just placed if MARKET; should be LimitPrice if LIMIT
            LimitPrice = order.LimitPrice,
            Quantity = order.Quantity,

            Security = order.Security,
            SecurityCode = order.SecurityCode,
            SecurityId = order.SecurityId,
        };

        Parse(jsonNode, receivedOrder, trades); // will change the order status accordingly
        if (receivedOrder.Status == OrderStatus.Unknown)
        {
            // binance's stop-loss-limit order when sent it returns without status
            if (receivedOrder.Type is OrderType.StopLimit or OrderType.TakeProfitLimit)
                receivedOrder.Status = OrderStatus.Sending;
        }
        var state = ExternalQueryStates.SendOrder(receivedOrder, content, connId, isOk).RecordTimes(rtt, swTotal);

        // raise events, and prevent other places to invoke events
        _broker.Enqueue(new EventInvokerTask(() =>
        {
            OrderPlaced?.Invoke(receivedOrder.IsGood, state);
            if (!trades.IsNullOrEmpty())
            {
                foreach (var trade in trades)
                {
                    if (!_processedExternalTradeIds.ThreadSafeContainsElseAdd(trade.ExternalTradeId))
                    {
                        TradeReceived?.Invoke(trade);
                    }
                }
            }
        }));
        return state;

        void Parse(JsonObject json, Order order, List<Trade> trades)
        {
            if (json.Count == 0)
                return;

            var code = json.GetInt("code");
            if (code is < 0 and not int.MinValue)
                return;
            order.ExternalOrderId = json.GetLong("orderId");
            order.Status = OrderStatuses.ParseBinance(json.GetString("status"));
            order.ExternalCreateTime = json.GetUtcFromUnixMs("transactTime"); // TODO not sure if workingTime is useful or not
            order.FilledQuantity = json.GetDecimal("executedQty");
            order.ExternalUpdateTime = json.GetUtcFromUnixMs("workingTime");
            order.UpdateTime = DateUtils.Max(order.ExternalCreateTime, order.ExternalUpdateTime);
            var isOperational = order.Action == OrderActionType.Operational;

            var fillsObj = json["fills"]?.AsArray();
            if (fillsObj != null && fillsObj.Count != 0)
            {
                for (int i = 0; i < fillsObj.Count; i++)
                {
                    JsonNode? item = fillsObj[i];
                    if (item == null)
                        continue;

                    var trade = new Trade
                    {
                        Id = _tradeIdGenerator.NewTimeBasedId,
                        AccountId = _context.AccountId,
                        ExternalTradeId = item.GetLong("tradeId"),
                        OrderId = order.Id,
                        ExternalOrderId = order.ExternalOrderId,
                        SecurityId = order.SecurityId,
                        SecurityCode = order.SecurityCode,
                        Price = item.GetDecimal("price"),
                        Quantity = item.GetDecimal("qty"),
                        Side = order.Side,
                        Fee = item.GetDecimal("commission"),
                        FeeAssetCode = item.GetString("commissionAsset"),
                        FeeAssetId = 0, // cannot be determined until executing in service layer logic
                        Time = order.ExternalUpdateTime,
                        PositionId = 0, // to be filled in later
                        IsOperational = isOperational,
                    };
                    trades.Add(trade);
                }
            }
        }
    }

    /// <summary>
    /// Cancel an order [SIGNED].
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> CancelOrder(Order order)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        var isOk = false;
        var swTotal = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/order";
        var parameters = new List<(string, string)>
        {
            ("symbol", order.SecurityCode),
            //("externalOrderId", order.ExternalOrderId.ToString()), // the Binance order Id takes precedence
            ("origClientOrderId", order.Id.ToString()),
            ("newClientOrderId", _cancelIdGenerator.NewTimeBasedId.ToString())
        };
        using var request = _requestBuilder.BuildSigned(HttpMethod.Delete, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.CancelOrder, ResultCode.CancelOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);

        var state = ExternalQueryStates.CancelOrders(order.SecurityCode, content, connId, order: order).RecordTimes(rtt, swTotal);
        if (state.ResultCode == ResultCode.CancelOrderOk)
        {
            // var cancelId = rootObj.GetLong("clientOrderId"); // should be equal to the above newClientOrderId
            order.ExternalUpdateTime = json.GetUtcFromUnixMs("transactTime");
            order.Status = OrderStatuses.ParseBinance(json.GetString("status"));
        }

        // raise events
        OrderCancelled?.Invoke(isOk, state);

        return state;
    }

    /// <summary>
    /// Cancel all orders related to a security [SIGNED].
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> CancelAllOrders(Security security)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (!ValidateSecurity(security, ActionType.CancelOrder, out var errorState))
            return errorState;

        var swTotal = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/openOrders";
        var parameters = new List<(string, string)> { ("symbol", security.Code) };
        using var request = _requestBuilder.BuildSigned(HttpMethod.Delete, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonArray(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.CancelOrder, ResultCode.CancelOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);
        var orders = ParseOrders(json, security);
        return ExternalQueryStates.CancelOrders(security.Code, content, connId, orders).RecordTimes(rtt, swTotal);
    }

    /// <summary>
    /// Get recent trades in the market [NONE].
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetMarketTrades(Security security)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (!ValidateSecurity(security, ActionType.GetTrade, out var errorState))
            return errorState;

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/trades";
        var parameters = new List<(string, string)> { ("symbol", security.Code) };
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url, parameters);

        // example JSON: var content = @"[ { ""id"": 3180532392, ""price"": ""29177.98000000"", ""qty"": ""0.00350000"", ""quoteQty"": ""102.12293000"", ""time"": 1690533483093, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532393, ""price"": ""29177.99000000"", ""qty"": ""0.00404000"", ""quoteQty"": ""117.87907960"", ""time"": 1690533483544, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532394, ""price"": ""29177.99000000"", ""qty"": ""0.02633000"", ""quoteQty"": ""768.25647670"", ""time"": 1690533483550, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532395, ""price"": ""29177.99000000"", ""qty"": ""0.04295000"", ""quoteQty"": ""1253.19467050"", ""time"": 1690533483550, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532396, ""price"": ""29177.98000000"", ""qty"": ""0.00168000"", ""quoteQty"": ""49.01900640"", ""time"": 1690533483779, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532397, ""price"": ""29177.99000000"", ""qty"": ""0.02539000"", ""quoteQty"": ""740.82916610"", ""time"": 1690533484546, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532398, ""price"": ""29177.99000000"", ""qty"": ""0.04389000"", ""quoteQty"": ""1280.62198110"", ""time"": 1690533484546, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532399, ""price"": ""29177.98000000"", ""qty"": ""0.00326000"", ""quoteQty"": ""95.12021480"", ""time"": 1690533484628, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532400, ""price"": ""29177.99000000"", ""qty"": ""0.13890000"", ""quoteQty"": ""4052.82281100"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532401, ""price"": ""29177.99000000"", ""qty"": ""0.09605000"", ""quoteQty"": ""2802.54593950"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532402, ""price"": ""29177.99000000"", ""qty"": ""0.01087000"", ""quoteQty"": ""317.16475130"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532403, ""price"": ""29177.99000000"", ""qty"": ""0.12811000"", ""quoteQty"": ""3737.99229890"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532404, ""price"": ""29177.99000000"", ""qty"": ""0.05206000"", ""quoteQty"": ""1519.00615940"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532405, ""price"": ""29177.99000000"", ""qty"": ""0.00700000"", ""quoteQty"": ""204.24593000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532406, ""price"": ""29177.99000000"", ""qty"": ""0.06834000"", ""quoteQty"": ""1994.02383660"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532407, ""price"": ""29178.01000000"", ""qty"": ""0.00700000"", ""quoteQty"": ""204.24607000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532408, ""price"": ""29178.03000000"", ""qty"": ""0.00700000"", ""quoteQty"": ""204.24621000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532409, ""price"": ""29178.21000000"", ""qty"": ""0.00085000"", ""quoteQty"": ""24.80147850"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532410, ""price"": ""29178.27000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34261600"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532411, ""price"": ""29178.27000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26044300"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532412, ""price"": ""29178.34000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26050600"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532413, ""price"": ""29178.34000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34267200"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532414, ""price"": ""29178.35000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26051500"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532415, ""price"": ""29178.46000000"", ""qty"": ""0.03448000"", ""quoteQty"": ""1006.07330080"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532416, ""price"": ""29179.02000000"", ""qty"": ""0.05744000"", ""quoteQty"": ""1676.04290880"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532417, ""price"": ""29179.15000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26123500"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532418, ""price"": ""29179.15000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34332000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532419, ""price"": ""29179.22000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26129800"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532420, ""price"": ""29179.22000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34337600"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532421, ""price"": ""29179.23000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26130700"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532422, ""price"": ""29179.45000000"", ""qty"": ""0.04625000"", ""quoteQty"": ""1349.54956250"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532423, ""price"": ""29179.78000000"", ""qty"": ""0.12062000"", ""quoteQty"": ""3519.66506360"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532424, ""price"": ""29179.78000000"", ""qty"": ""0.07108000"", ""quoteQty"": ""2074.09876240"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532425, ""price"": ""29180.00000000"", ""qty"": ""0.01065000"", ""quoteQty"": ""310.76700000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532426, ""price"": ""29180.03000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26202700"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532427, ""price"": ""29180.03000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34402400"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532428, ""price"": ""29180.10000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26209000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532429, ""price"": ""29180.10000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34408000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532430, ""price"": ""29180.10000000"", ""qty"": ""0.34280000"", ""quoteQty"": ""10002.93828000"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532431, ""price"": ""29180.11000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26209900"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532432, ""price"": ""29180.16000000"", ""qty"": ""0.00240000"", ""quoteQty"": ""70.03238400"", ""time"": 1690533485347, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532433, ""price"": ""29180.61000000"", ""qty"": ""0.00202000"", ""quoteQty"": ""58.94483220"", ""time"": 1690533485349, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532434, ""price"": ""29180.89000000"", ""qty"": ""0.06147000"", ""quoteQty"": ""1793.74930830"", ""time"": 1690533485362, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532435, ""price"": ""29180.89000000"", ""qty"": ""0.06834000"", ""quoteQty"": ""1994.22202260"", ""time"": 1690533485409, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532436, ""price"": ""29180.89000000"", ""qty"": ""0.13713000"", ""quoteQty"": ""4001.57544570"", ""time"": 1690533485409, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532437, ""price"": ""29180.89000000"", ""qty"": ""0.08200000"", ""quoteQty"": ""2392.83298000"", ""time"": 1690533485409, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532438, ""price"": ""29180.89000000"", ""qty"": ""0.64283000"", ""quoteQty"": ""18758.35151870"", ""time"": 1690533485409, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532439, ""price"": ""29180.89000000"", ""qty"": ""0.04400000"", ""quoteQty"": ""1283.95916000"", ""time"": 1690533485434, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532440, ""price"": ""29180.89000000"", ""qty"": ""0.01700000"", ""quoteQty"": ""496.07513000"", ""time"": 1690533485443, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532441, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533485544, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532442, ""price"": ""29180.88000000"", ""qty"": ""0.00206000"", ""quoteQty"": ""60.11261280"", ""time"": 1690533486019, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532443, ""price"": ""29180.88000000"", ""qty"": ""0.00435000"", ""quoteQty"": ""126.93682800"", ""time"": 1690533486829, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532444, ""price"": ""29180.88000000"", ""qty"": ""0.01386000"", ""quoteQty"": ""404.44699680"", ""time"": 1690533486829, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532445, ""price"": ""29180.88000000"", ""qty"": ""0.00304000"", ""quoteQty"": ""88.70987520"", ""time"": 1690533486829, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532446, ""price"": ""29180.88000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59385120"", ""time"": 1690533486871, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532447, ""price"": ""29180.89000000"", ""qty"": ""0.00056000"", ""quoteQty"": ""16.34129840"", ""time"": 1690533487287, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532448, ""price"": ""29180.89000000"", ""qty"": ""0.01000000"", ""quoteQty"": ""291.80890000"", ""time"": 1690533487867, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532449, ""price"": ""29180.89000000"", ""qty"": ""0.02193000"", ""quoteQty"": ""639.93691770"", ""time"": 1690533488567, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532450, ""price"": ""29180.88000000"", ""qty"": ""0.00845000"", ""quoteQty"": ""246.57843600"", ""time"": 1690533489119, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532451, ""price"": ""29180.89000000"", ""qty"": ""0.02192000"", ""quoteQty"": ""639.64510880"", ""time"": 1690533490567, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532452, ""price"": ""29180.89000000"", ""qty"": ""0.00136000"", ""quoteQty"": ""39.68601040"", ""time"": 1690533490785, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532453, ""price"": ""29180.88000000"", ""qty"": ""0.02541000"", ""quoteQty"": ""741.48616080"", ""time"": 1690533491005, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532454, ""price"": ""29180.88000000"", ""qty"": ""0.15105000"", ""quoteQty"": ""4407.77192400"", ""time"": 1690533491005, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532455, ""price"": ""29180.89000000"", ""qty"": ""0.02192000"", ""quoteQty"": ""639.64510880"", ""time"": 1690533491567, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532456, ""price"": ""29180.89000000"", ""qty"": ""0.05203000"", ""quoteQty"": ""1518.28170670"", ""time"": 1690533491567, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532457, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533492552, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532458, ""price"": ""29180.88000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84299840"", ""time"": 1690533493873, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532459, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533494551, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532460, ""price"": ""29180.88000000"", ""qty"": ""0.00154000"", ""quoteQty"": ""44.93855520"", ""time"": 1690533495066, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532461, ""price"": ""29180.89000000"", ""qty"": ""0.03174000"", ""quoteQty"": ""926.20144860"", ""time"": 1690533495558, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532462, ""price"": ""29180.89000000"", ""qty"": ""0.01087000"", ""quoteQty"": ""317.19627430"", ""time"": 1690533495558, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532463, ""price"": ""29180.89000000"", ""qty"": ""0.02666000"", ""quoteQty"": ""777.96252740"", ""time"": 1690533495558, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532464, ""price"": ""29180.88000000"", ""qty"": ""0.00171000"", ""quoteQty"": ""49.89930480"", ""time"": 1690533495861, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532465, ""price"": ""29180.88000000"", ""qty"": ""0.00088000"", ""quoteQty"": ""25.67917440"", ""time"": 1690533496595, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532466, ""price"": ""29180.88000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59385120"", ""time"": 1690533496980, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532467, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533497066, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532468, ""price"": ""29180.88000000"", ""qty"": ""0.00060000"", ""quoteQty"": ""17.50852800"", ""time"": 1690533497473, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532469, ""price"": ""29180.89000000"", ""qty"": ""0.00072000"", ""quoteQty"": ""21.01024080"", ""time"": 1690533497551, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532470, ""price"": ""29180.88000000"", ""qty"": ""0.00291000"", ""quoteQty"": ""84.91636080"", ""time"": 1690533497841, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532471, ""price"": ""29180.89000000"", ""qty"": ""0.00106000"", ""quoteQty"": ""30.93174340"", ""time"": 1690533498553, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532472, ""price"": ""29180.89000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59385860"", ""time"": 1690533498742, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532473, ""price"": ""29180.89000000"", ""qty"": ""0.00795000"", ""quoteQty"": ""231.98807550"", ""time"": 1690533498802, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532474, ""price"": ""29180.88000000"", ""qty"": ""0.00715000"", ""quoteQty"": ""208.64329200"", ""time"": 1690533498999, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532475, ""price"": ""29180.89000000"", ""qty"": ""0.00342000"", ""quoteQty"": ""99.79864380"", ""time"": 1690533499118, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532476, ""price"": ""29180.88000000"", ""qty"": ""0.06781000"", ""quoteQty"": ""1978.75547280"", ""time"": 1690533499620, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532477, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533500071, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532478, ""price"": ""29180.88000000"", ""qty"": ""0.20000000"", ""quoteQty"": ""5836.17600000"", ""time"": 1690533500150, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532479, ""price"": ""29180.89000000"", ""qty"": ""0.00119000"", ""quoteQty"": ""34.72525910"", ""time"": 1690533500268, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532480, ""price"": ""29180.88000000"", ""qty"": ""0.01247000"", ""quoteQty"": ""363.88557360"", ""time"": 1690533500726, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532481, ""price"": ""29180.89000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13481410"", ""time"": 1690533501954, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532482, ""price"": ""29180.89000000"", ""qty"": ""0.00132000"", ""quoteQty"": ""38.51877480"", ""time"": 1690533501955, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532483, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533502073, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532484, ""price"": ""29180.89000000"", ""qty"": ""0.00130000"", ""quoteQty"": ""37.93515700"", ""time"": 1690533502119, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532485, ""price"": ""29180.89000000"", ""qty"": ""0.01697000"", ""quoteQty"": ""495.19970330"", ""time"": 1690533502609, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532486, ""price"": ""29180.88000000"", ""qty"": ""0.00889000"", ""quoteQty"": ""259.41802320"", ""time"": 1690533502627, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532487, ""price"": ""29180.88000000"", ""qty"": ""0.02225000"", ""quoteQty"": ""649.27458000"", ""time"": 1690533503037, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532488, ""price"": ""29180.89000000"", ""qty"": ""0.00086000"", ""quoteQty"": ""25.09556540"", ""time"": 1690533503054, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532489, ""price"": ""29180.88000000"", ""qty"": ""0.00076000"", ""quoteQty"": ""22.17746880"", ""time"": 1690533503067, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532490, ""price"": ""29180.88000000"", ""qty"": ""0.00379000"", ""quoteQty"": ""110.59553520"", ""time"": 1690533503243, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532491, ""price"": ""29180.89000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13481410"", ""time"": 1690533503343, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532492, ""price"": ""29180.88000000"", ""qty"": ""0.00495000"", ""quoteQty"": ""144.44535600"", ""time"": 1690533503663, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532493, ""price"": ""29180.88000000"", ""qty"": ""0.00045000"", ""quoteQty"": ""13.13139600"", ""time"": 1690533503864, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532494, ""price"": ""29180.89000000"", ""qty"": ""0.06927000"", ""quoteQty"": ""2021.36025030"", ""time"": 1690533505575, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532495, ""price"": ""29180.89000000"", ""qty"": ""0.00059000"", ""quoteQty"": ""17.21672510"", ""time"": 1690533506075, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532496, ""price"": ""29180.88000000"", ""qty"": ""0.01017000"", ""quoteQty"": ""296.76954960"", ""time"": 1690533506685, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532497, ""price"": ""29180.89000000"", ""qty"": ""0.00451000"", ""quoteQty"": ""131.60581390"", ""time"": 1690533507005, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532498, ""price"": ""29180.89000000"", ""qty"": ""0.01012000"", ""quoteQty"": ""295.31060680"", ""time"": 1690533507990, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532499, ""price"": ""29180.89000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13481410"", ""time"": 1690533508019, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532500, ""price"": ""29180.89000000"", ""qty"": ""0.00188000"", ""quoteQty"": ""54.86007320"", ""time"": 1690533508124, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532501, ""price"": ""29180.88000000"", ""qty"": ""0.04119000"", ""quoteQty"": ""1201.96044720"", ""time"": 1690533508716, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532502, ""price"": ""29180.88000000"", ""qty"": ""0.01435000"", ""quoteQty"": ""418.74562800"", ""time"": 1690533509067, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532503, ""price"": ""29180.89000000"", ""qty"": ""0.00566000"", ""quoteQty"": ""165.16383740"", ""time"": 1690533509845, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532504, ""price"": ""29180.88000000"", ""qty"": ""0.02874000"", ""quoteQty"": ""838.65849120"", ""time"": 1690533510065, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532505, ""price"": ""29180.88000000"", ""qty"": ""0.00042000"", ""quoteQty"": ""12.25596960"", ""time"": 1690533511573, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532506, ""price"": ""29180.88000000"", ""qty"": ""0.41650000"", ""quoteQty"": ""12153.83652000"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532507, ""price"": ""29180.88000000"", ""qty"": ""0.00309000"", ""quoteQty"": ""90.16891920"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532508, ""price"": ""29180.88000000"", ""qty"": ""0.00233000"", ""quoteQty"": ""67.99145040"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532509, ""price"": ""29180.88000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""74.99486160"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532510, ""price"": ""29180.88000000"", ""qty"": ""0.00222000"", ""quoteQty"": ""64.78155360"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532511, ""price"": ""29180.88000000"", ""qty"": ""0.05942000"", ""quoteQty"": ""1733.92788960"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532512, ""price"": ""29180.88000000"", ""qty"": ""0.00225000"", ""quoteQty"": ""65.65698000"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532513, ""price"": ""29180.88000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""74.99486160"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532514, ""price"": ""29180.88000000"", ""qty"": ""0.00287000"", ""quoteQty"": ""83.74912560"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532515, ""price"": ""29180.88000000"", ""qty"": ""0.00312000"", ""quoteQty"": ""91.04434560"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532516, ""price"": ""29180.88000000"", ""qty"": ""0.00222000"", ""quoteQty"": ""64.78155360"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532517, ""price"": ""29180.88000000"", ""qty"": ""0.18152000"", ""quoteQty"": ""5296.91333760"", ""time"": 1690533511602, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532518, ""price"": ""29180.89000000"", ""qty"": ""0.00321000"", ""quoteQty"": ""93.67065690"", ""time"": 1690533512317, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532519, ""price"": ""29180.89000000"", ""qty"": ""0.00035000"", ""quoteQty"": ""10.21331150"", ""time"": 1690533512447, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532520, ""price"": ""29180.88000000"", ""qty"": ""0.00076000"", ""quoteQty"": ""22.17746880"", ""time"": 1690533512739, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532521, ""price"": ""29180.89000000"", ""qty"": ""0.00081000"", ""quoteQty"": ""23.63652090"", ""time"": 1690533514852, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532522, ""price"": ""29180.89000000"", ""qty"": ""0.00167000"", ""quoteQty"": ""48.73208630"", ""time"": 1690533514874, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532523, ""price"": ""29180.89000000"", ""qty"": ""0.00125000"", ""quoteQty"": ""36.47611250"", ""time"": 1690533514876, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532524, ""price"": ""29180.89000000"", ""qty"": ""0.15000000"", ""quoteQty"": ""4377.13350000"", ""time"": 1690533514907, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532525, ""price"": ""29180.89000000"", ""qty"": ""0.31399000"", ""quoteQty"": ""9162.50765110"", ""time"": 1690533514907, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532526, ""price"": ""29180.89000000"", ""qty"": ""0.28000000"", ""quoteQty"": ""8170.64920000"", ""time"": 1690533514907, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532527, ""price"": ""29180.89000000"", ""qty"": ""0.66446000"", ""quoteQty"": ""19389.53416940"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532528, ""price"": ""29180.89000000"", ""qty"": ""0.01710000"", ""quoteQty"": ""498.99321900"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532529, ""price"": ""29180.89000000"", ""qty"": ""0.00112000"", ""quoteQty"": ""32.68259680"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532530, ""price"": ""29180.89000000"", ""qty"": ""0.08136000"", ""quoteQty"": ""2374.15721040"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532531, ""price"": ""29180.89000000"", ""qty"": ""0.00233000"", ""quoteQty"": ""67.99147370"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532532, ""price"": ""29180.89000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""74.99488730"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532533, ""price"": ""29180.89000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""74.99488730"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532534, ""price"": ""29180.89000000"", ""qty"": ""0.00222000"", ""quoteQty"": ""64.78157580"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532535, ""price"": ""29180.89000000"", ""qty"": ""0.00224000"", ""quoteQty"": ""65.36519360"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532536, ""price"": ""29180.89000000"", ""qty"": ""0.00312000"", ""quoteQty"": ""91.04437680"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532537, ""price"": ""29180.89000000"", ""qty"": ""0.00096000"", ""quoteQty"": ""28.01365440"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532538, ""price"": ""29180.89000000"", ""qty"": ""0.00246000"", ""quoteQty"": ""71.78498940"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532539, ""price"": ""29180.89000000"", ""qty"": ""0.01675000"", ""quoteQty"": ""488.77990750"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532540, ""price"": ""29180.89000000"", ""qty"": ""0.51449000"", ""quoteQty"": ""15013.27609610"", ""time"": 1690533514915, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532541, ""price"": ""29180.89000000"", ""qty"": ""0.00057000"", ""quoteQty"": ""16.63310730"", ""time"": 1690533514918, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532542, ""price"": ""29180.89000000"", ""qty"": ""0.29995000"", ""quoteQty"": ""8752.80795550"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532543, ""price"": ""29180.90000000"", ""qty"": ""0.06834000"", ""quoteQty"": ""1994.22270600"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532544, ""price"": ""29180.91000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34472800"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532545, ""price"": ""29180.91000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26281900"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532546, ""price"": ""29180.98000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34478400"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532547, ""price"": ""29180.98000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26288200"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532548, ""price"": ""29180.99000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26289100"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532549, ""price"": ""29181.53000000"", ""qty"": ""0.00135000"", ""quoteQty"": ""39.39506550"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532550, ""price"": ""29181.79000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34543200"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532551, ""price"": ""29181.79000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26361100"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532552, ""price"": ""29181.86000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26367400"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532553, ""price"": ""29181.86000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34548800"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532554, ""price"": ""29181.87000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26368300"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532555, ""price"": ""29181.87000000"", ""qty"": ""0.04702000"", ""quoteQty"": ""1372.13152740"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532556, ""price"": ""29182.00000000"", ""qty"": ""0.13713000"", ""quoteQty"": ""4001.72766000"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532557, ""price"": ""29182.29000000"", ""qty"": ""0.00035000"", ""quoteQty"": ""10.21380150"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532558, ""price"": ""29182.67000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26440300"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532559, ""price"": ""29182.67000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34613600"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532560, ""price"": ""29182.74000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34619200"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532561, ""price"": ""29182.74000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26446600"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532562, ""price"": ""29182.75000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26447500"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532563, ""price"": ""29182.95000000"", ""qty"": ""0.24391000"", ""quoteQty"": ""7118.01333450"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532564, ""price"": ""29182.97000000"", ""qty"": ""0.00549000"", ""quoteQty"": ""160.21450530"", ""time"": 1690533514920, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532565, ""price"": ""29182.97000000"", ""qty"": ""0.01198000"", ""quoteQty"": ""349.61198060"", ""time"": 1690533514927, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532566, ""price"": ""29183.17000000"", ""qty"": ""0.00250000"", ""quoteQty"": ""72.95792500"", ""time"": 1690533514927, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532567, ""price"": ""29183.22000000"", ""qty"": ""0.00042000"", ""quoteQty"": ""12.25695240"", ""time"": 1690533514931, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532568, ""price"": ""29183.22000000"", ""qty"": ""0.30865000"", ""quoteQty"": ""9007.40085300"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532569, ""price"": ""29183.22000000"", ""qty"": ""0.19084000"", ""quoteQty"": ""5569.32570480"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532570, ""price"": ""29183.22000000"", ""qty"": ""2.68690000"", ""quoteQty"": ""78412.39381800"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532571, ""price"": ""29183.22000000"", ""qty"": ""0.04795000"", ""quoteQty"": ""1399.33539900"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532572, ""price"": ""29183.22000000"", ""qty"": ""0.00309000"", ""quoteQty"": ""90.17614980"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532573, ""price"": ""29183.22000000"", ""qty"": ""0.13712000"", ""quoteQty"": ""4001.60312640"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532574, ""price"": ""29183.22000000"", ""qty"": ""0.00222000"", ""quoteQty"": ""64.78674840"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532575, ""price"": ""29183.22000000"", ""qty"": ""0.00287000"", ""quoteQty"": ""83.75584140"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532576, ""price"": ""29183.22000000"", ""qty"": ""0.34277000"", ""quoteQty"": ""10003.13231940"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532577, ""price"": ""29183.22000000"", ""qty"": ""0.00300000"", ""quoteQty"": ""87.54966000"", ""time"": 1690533515000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532578, ""price"": ""29183.21000000"", ""qty"": ""0.00724000"", ""quoteQty"": ""211.28644040"", ""time"": 1690533515001, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532579, ""price"": ""29183.21000000"", ""qty"": ""0.00749000"", ""quoteQty"": ""218.58224290"", ""time"": 1690533515001, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532580, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533515036, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532581, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533515068, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532582, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533515081, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532583, ""price"": ""29183.22000000"", ""qty"": ""0.00179000"", ""quoteQty"": ""52.23796380"", ""time"": 1690533515099, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532584, ""price"": ""29183.22000000"", ""qty"": ""0.00120000"", ""quoteQty"": ""35.01986400"", ""time"": 1690533515102, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532585, ""price"": ""29183.21000000"", ""qty"": ""0.02640000"", ""quoteQty"": ""770.43674400"", ""time"": 1690533515166, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532586, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533515359, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532587, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533515466, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532588, ""price"": ""29183.22000000"", ""qty"": ""0.00056000"", ""quoteQty"": ""16.34260320"", ""time"": 1690533515474, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532589, ""price"": ""29183.22000000"", ""qty"": ""0.03426000"", ""quoteQty"": ""999.81711720"", ""time"": 1690533515709, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532590, ""price"": ""29183.22000000"", ""qty"": ""0.01786000"", ""quoteQty"": ""521.21230920"", ""time"": 1690533515767, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532591, ""price"": ""29183.22000000"", ""qty"": ""0.02014000"", ""quoteQty"": ""587.75005080"", ""time"": 1690533515767, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532592, ""price"": ""29183.21000000"", ""qty"": ""0.00178000"", ""quoteQty"": ""51.94611380"", ""time"": 1690533516664, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532593, ""price"": ""29183.22000000"", ""qty"": ""0.00159000"", ""quoteQty"": ""46.40131980"", ""time"": 1690533517043, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532594, ""price"": ""29183.22000000"", ""qty"": ""0.00372000"", ""quoteQty"": ""108.56157840"", ""time"": 1690533517925, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532595, ""price"": ""29183.22000000"", ""qty"": ""0.00044000"", ""quoteQty"": ""12.84061680"", ""time"": 1690533518082, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532596, ""price"": ""29183.21000000"", ""qty"": ""0.00078000"", ""quoteQty"": ""22.76290380"", ""time"": 1690533518124, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532597, ""price"": ""29183.21000000"", ""qty"": ""0.00081000"", ""quoteQty"": ""23.63840010"", ""time"": 1690533518267, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532598, ""price"": ""29183.21000000"", ""qty"": ""0.00039000"", ""quoteQty"": ""11.38145190"", ""time"": 1690533518501, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532599, ""price"": ""29183.21000000"", ""qty"": ""0.06604000"", ""quoteQty"": ""1927.25918840"", ""time"": 1690533518503, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532600, ""price"": ""29183.22000000"", ""qty"": ""0.00040000"", ""quoteQty"": ""11.67328800"", ""time"": 1690533518511, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532601, ""price"": ""29183.21000000"", ""qty"": ""0.00038000"", ""quoteQty"": ""11.08961980"", ""time"": 1690533518512, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532602, ""price"": ""29183.22000000"", ""qty"": ""0.11083000"", ""quoteQty"": ""3234.37627260"", ""time"": 1690533521152, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532603, ""price"": ""29183.22000000"", ""qty"": ""0.00300000"", ""quoteQty"": ""87.54966000"", ""time"": 1690533521152, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532604, ""price"": ""29183.22000000"", ""qty"": ""0.01403000"", ""quoteQty"": ""409.44057660"", ""time"": 1690533521152, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532605, ""price"": ""29183.22000000"", ""qty"": ""0.00958000"", ""quoteQty"": ""279.57524760"", ""time"": 1690533521190, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532606, ""price"": ""29183.22000000"", ""qty"": ""0.00363000"", ""quoteQty"": ""105.93508860"", ""time"": 1690533521190, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532607, ""price"": ""29183.22000000"", ""qty"": ""0.00957000"", ""quoteQty"": ""279.28341540"", ""time"": 1690533521279, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532608, ""price"": ""29183.21000000"", ""qty"": ""0.00480000"", ""quoteQty"": ""140.07940800"", ""time"": 1690533522356, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532609, ""price"": ""29183.21000000"", ""qty"": ""0.02714000"", ""quoteQty"": ""792.03231940"", ""time"": 1690533522447, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532610, ""price"": ""29183.22000000"", ""qty"": ""0.00155000"", ""quoteQty"": ""45.23399100"", ""time"": 1690533522856, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532611, ""price"": ""29183.22000000"", ""qty"": ""0.02164000"", ""quoteQty"": ""631.52488080"", ""time"": 1690533522987, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532612, ""price"": ""29183.22000000"", ""qty"": ""0.00760000"", ""quoteQty"": ""221.79247200"", ""time"": 1690533524988, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532613, ""price"": ""29183.22000000"", ""qty"": ""0.01258000"", ""quoteQty"": ""367.12490760"", ""time"": 1690533525084, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532614, ""price"": ""29183.22000000"", ""qty"": ""0.00333000"", ""quoteQty"": ""97.18012260"", ""time"": 1690533525395, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532615, ""price"": ""29183.22000000"", ""qty"": ""0.00746000"", ""quoteQty"": ""217.70682120"", ""time"": 1690533526475, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532616, ""price"": ""29183.21000000"", ""qty"": ""0.00137000"", ""quoteQty"": ""39.98099770"", ""time"": 1690533526687, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532617, ""price"": ""29183.22000000"", ""qty"": ""0.01257000"", ""quoteQty"": ""366.83307540"", ""time"": 1690533527084, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532618, ""price"": ""29183.22000000"", ""qty"": ""0.00037000"", ""quoteQty"": ""10.79779140"", ""time"": 1690533527856, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532619, ""price"": ""29183.22000000"", ""qty"": ""0.01257000"", ""quoteQty"": ""366.83307540"", ""time"": 1690533528084, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532620, ""price"": ""29183.22000000"", ""qty"": ""0.00757000"", ""quoteQty"": ""220.91697540"", ""time"": 1690533528601, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532621, ""price"": ""29183.21000000"", ""qty"": ""0.00310000"", ""quoteQty"": ""90.46795100"", ""time"": 1690533529797, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532622, ""price"": ""29183.22000000"", ""qty"": ""0.00039000"", ""quoteQty"": ""11.38145580"", ""time"": 1690533530048, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532623, ""price"": ""29183.22000000"", ""qty"": ""0.06604000"", ""quoteQty"": ""1927.25984880"", ""time"": 1690533530054, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532624, ""price"": ""29183.22000000"", ""qty"": ""0.00038000"", ""quoteQty"": ""11.08962360"", ""time"": 1690533530115, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532625, ""price"": ""29183.22000000"", ""qty"": ""0.01781000"", ""quoteQty"": ""519.75314820"", ""time"": 1690533531224, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532626, ""price"": ""29183.21000000"", ""qty"": ""0.00241000"", ""quoteQty"": ""70.33153610"", ""time"": 1690533531355, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532627, ""price"": ""29183.22000000"", ""qty"": ""0.00050000"", ""quoteQty"": ""14.59161000"", ""time"": 1690533532311, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532628, ""price"": ""29183.21000000"", ""qty"": ""0.00685000"", ""quoteQty"": ""199.90498850"", ""time"": 1690533532599, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532629, ""price"": ""29183.21000000"", ""qty"": ""0.00481000"", ""quoteQty"": ""140.37124010"", ""time"": 1690533532623, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532630, ""price"": ""29183.22000000"", ""qty"": ""0.00412000"", ""quoteQty"": ""120.23486640"", ""time"": 1690533534439, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532631, ""price"": ""29183.22000000"", ""qty"": ""0.00524000"", ""quoteQty"": ""152.92007280"", ""time"": 1690533535239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532632, ""price"": ""29183.21000000"", ""qty"": ""0.00990000"", ""quoteQty"": ""288.91377900"", ""time"": 1690533536227, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532633, ""price"": ""29183.22000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59558280"", ""time"": 1690533537544, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532634, ""price"": ""29183.22000000"", ""qty"": ""0.00523000"", ""quoteQty"": ""152.62824060"", ""time"": 1690533538139, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532635, ""price"": ""29183.22000000"", ""qty"": ""0.00105000"", ""quoteQty"": ""30.64238100"", ""time"": 1690533538174, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532636, ""price"": ""29183.21000000"", ""qty"": ""0.03005000"", ""quoteQty"": ""876.95546050"", ""time"": 1690533538194, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532637, ""price"": ""29183.22000000"", ""qty"": ""0.00822000"", ""quoteQty"": ""239.88606840"", ""time"": 1690533539320, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532638, ""price"": ""29183.22000000"", ""qty"": ""0.00523000"", ""quoteQty"": ""152.62824060"", ""time"": 1690533539642, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532639, ""price"": ""29183.22000000"", ""qty"": ""0.00101000"", ""quoteQty"": ""29.47505220"", ""time"": 1690533539975, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532640, ""price"": ""29183.22000000"", ""qty"": ""0.00131000"", ""quoteQty"": ""38.23001820"", ""time"": 1690533539976, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532641, ""price"": ""29183.22000000"", ""qty"": ""0.00173000"", ""quoteQty"": ""50.48697060"", ""time"": 1690533539977, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532642, ""price"": ""29183.21000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""75.00084970"", ""time"": 1690533539978, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532643, ""price"": ""29183.22000000"", ""qty"": ""0.00062000"", ""quoteQty"": ""18.09359640"", ""time"": 1690533539979, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532644, ""price"": ""29183.22000000"", ""qty"": ""0.00064000"", ""quoteQty"": ""18.67726080"", ""time"": 1690533539979, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532645, ""price"": ""29183.21000000"", ""qty"": ""0.00114000"", ""quoteQty"": ""33.26885940"", ""time"": 1690533539979, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532646, ""price"": ""29183.21000000"", ""qty"": ""0.00078000"", ""quoteQty"": ""22.76290380"", ""time"": 1690533539979, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532647, ""price"": ""29183.21000000"", ""qty"": ""0.00136000"", ""quoteQty"": ""39.68916560"", ""time"": 1690533539981, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532648, ""price"": ""29183.22000000"", ""qty"": ""0.00045000"", ""quoteQty"": ""13.13244900"", ""time"": 1690533539983, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532649, ""price"": ""29183.21000000"", ""qty"": ""0.00246000"", ""quoteQty"": ""71.79069660"", ""time"": 1690533539986, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532650, ""price"": ""29183.21000000"", ""qty"": ""0.00142000"", ""quoteQty"": ""41.44015820"", ""time"": 1690533539987, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532651, ""price"": ""29183.22000000"", ""qty"": ""0.00119000"", ""quoteQty"": ""34.72803180"", ""time"": 1690533539988, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532652, ""price"": ""29183.22000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26489800"", ""time"": 1690533539991, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532653, ""price"": ""29183.21000000"", ""qty"": ""0.00140000"", ""quoteQty"": ""40.85649400"", ""time"": 1690533539991, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532654, ""price"": ""29183.21000000"", ""qty"": ""0.00082000"", ""quoteQty"": ""23.93023220"", ""time"": 1690533539992, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532655, ""price"": ""29183.21000000"", ""qty"": ""0.00126000"", ""quoteQty"": ""36.77084460"", ""time"": 1690533539992, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532656, ""price"": ""29183.21000000"", ""qty"": ""0.00182000"", ""quoteQty"": ""53.11344220"", ""time"": 1690533539992, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532657, ""price"": ""29183.22000000"", ""qty"": ""0.00044000"", ""quoteQty"": ""12.84061680"", ""time"": 1690533539994, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532658, ""price"": ""29183.22000000"", ""qty"": ""0.00054000"", ""quoteQty"": ""15.75893880"", ""time"": 1690533539994, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532659, ""price"": ""29183.22000000"", ""qty"": ""0.00257000"", ""quoteQty"": ""75.00087540"", ""time"": 1690533539995, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532660, ""price"": ""29183.21000000"", ""qty"": ""0.00338000"", ""quoteQty"": ""98.63924980"", ""time"": 1690533539996, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532661, ""price"": ""29183.22000000"", ""qty"": ""0.00044000"", ""quoteQty"": ""12.84061680"", ""time"": 1690533539996, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532662, ""price"": ""29183.21000000"", ""qty"": ""0.00062000"", ""quoteQty"": ""18.09359020"", ""time"": 1690533539996, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532663, ""price"": ""29183.22000000"", ""qty"": ""0.00224000"", ""quoteQty"": ""65.37041280"", ""time"": 1690533539996, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532664, ""price"": ""29183.21000000"", ""qty"": ""0.00077000"", ""quoteQty"": ""22.47107170"", ""time"": 1690533539997, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532665, ""price"": ""29183.22000000"", ""qty"": ""0.00035000"", ""quoteQty"": ""10.21412700"", ""time"": 1690533539997, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532666, ""price"": ""29183.22000000"", ""qty"": ""0.00256000"", ""quoteQty"": ""74.70904320"", ""time"": 1690533539998, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532667, ""price"": ""29183.21000000"", ""qty"": ""0.00095000"", ""quoteQty"": ""27.72404950"", ""time"": 1690533539999, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532668, ""price"": ""29183.21000000"", ""qty"": ""0.00097000"", ""quoteQty"": ""28.30771370"", ""time"": 1690533539999, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532669, ""price"": ""29183.21000000"", ""qty"": ""0.00122000"", ""quoteQty"": ""35.60351620"", ""time"": 1690533540000, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532670, ""price"": ""29183.22000000"", ""qty"": ""0.00203000"", ""quoteQty"": ""59.24193660"", ""time"": 1690533540000, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532671, ""price"": ""29183.21000000"", ""qty"": ""0.00127000"", ""quoteQty"": ""37.06267670"", ""time"": 1690533540002, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532672, ""price"": ""29183.21000000"", ""qty"": ""0.00243000"", ""quoteQty"": ""70.91520030"", ""time"": 1690533540002, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532673, ""price"": ""29183.21000000"", ""qty"": ""0.00088000"", ""quoteQty"": ""25.68122480"", ""time"": 1690533540003, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532674, ""price"": ""29183.21000000"", ""qty"": ""0.00049000"", ""quoteQty"": ""14.29977290"", ""time"": 1690533540004, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532675, ""price"": ""29183.21000000"", ""qty"": ""0.00055000"", ""quoteQty"": ""16.05076550"", ""time"": 1690533540006, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532676, ""price"": ""29183.22000000"", ""qty"": ""0.00052000"", ""quoteQty"": ""15.17527440"", ""time"": 1690533540007, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532677, ""price"": ""29183.21000000"", ""qty"": ""0.00142000"", ""quoteQty"": ""41.44015820"", ""time"": 1690533540007, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532678, ""price"": ""29183.22000000"", ""qty"": ""0.00103000"", ""quoteQty"": ""30.05871660"", ""time"": 1690533540008, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532679, ""price"": ""29183.22000000"", ""qty"": ""0.00073000"", ""quoteQty"": ""21.30375060"", ""time"": 1690533540009, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532680, ""price"": ""29183.21000000"", ""qty"": ""0.00065000"", ""quoteQty"": ""18.96908650"", ""time"": 1690533540009, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532681, ""price"": ""29183.21000000"", ""qty"": ""0.00063000"", ""quoteQty"": ""18.38542230"", ""time"": 1690533540012, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532682, ""price"": ""29183.22000000"", ""qty"": ""0.00057000"", ""quoteQty"": ""16.63443540"", ""time"": 1690533540012, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532683, ""price"": ""29183.22000000"", ""qty"": ""0.00240000"", ""quoteQty"": ""70.03972800"", ""time"": 1690533540017, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532684, ""price"": ""29183.22000000"", ""qty"": ""0.00086000"", ""quoteQty"": ""25.09756920"", ""time"": 1690533540018, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532685, ""price"": ""29183.22000000"", ""qty"": ""0.00071000"", ""quoteQty"": ""20.72008620"", ""time"": 1690533540022, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532686, ""price"": ""29183.22000000"", ""qty"": ""0.00088000"", ""quoteQty"": ""25.68123360"", ""time"": 1690533540023, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532687, ""price"": ""29183.22000000"", ""qty"": ""0.00132000"", ""quoteQty"": ""38.52185040"", ""time"": 1690533540023, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532688, ""price"": ""29183.21000000"", ""qty"": ""0.00050000"", ""quoteQty"": ""14.59160500"", ""time"": 1690533540031, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532689, ""price"": ""29183.21000000"", ""qty"": ""0.00072000"", ""quoteQty"": ""21.01191120"", ""time"": 1690533540031, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532690, ""price"": ""29183.21000000"", ""qty"": ""0.00091000"", ""quoteQty"": ""26.55672110"", ""time"": 1690533540031, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532691, ""price"": ""29183.22000000"", ""qty"": ""0.00089000"", ""quoteQty"": ""25.97306580"", ""time"": 1690533540039, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532692, ""price"": ""29183.22000000"", ""qty"": ""0.00281000"", ""quoteQty"": ""82.00484820"", ""time"": 1690533540041, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532693, ""price"": ""29183.21000000"", ""qty"": ""0.00099000"", ""quoteQty"": ""28.89137790"", ""time"": 1690533540042, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532694, ""price"": ""29183.21000000"", ""qty"": ""0.00052000"", ""quoteQty"": ""15.17526920"", ""time"": 1690533540044, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532695, ""price"": ""29183.22000000"", ""qty"": ""0.00215000"", ""quoteQty"": ""62.74392300"", ""time"": 1690533540044, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532696, ""price"": ""29183.22000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59558280"", ""time"": 1690533540052, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532697, ""price"": ""29183.22000000"", ""qty"": ""0.02637000"", ""quoteQty"": ""769.56151140"", ""time"": 1690533540111, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532698, ""price"": ""29183.22000000"", ""qty"": ""0.04289000"", ""quoteQty"": ""1251.66830580"", ""time"": 1690533540111, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532699, ""price"": ""29183.22000000"", ""qty"": ""0.00523000"", ""quoteQty"": ""152.62824060"", ""time"": 1690533540986, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532700, ""price"": ""29183.22000000"", ""qty"": ""0.00988000"", ""quoteQty"": ""288.33021360"", ""time"": 1690533541117, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532701, ""price"": ""29183.21000000"", ""qty"": ""0.06853000"", ""quoteQty"": ""1999.92538130"", ""time"": 1690533541138, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532702, ""price"": ""29183.22000000"", ""qty"": ""0.06900000"", ""quoteQty"": ""2013.64218000"", ""time"": 1690533541533, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532703, ""price"": ""29183.22000000"", ""qty"": ""0.06190000"", ""quoteQty"": ""1806.44131800"", ""time"": 1690533541534, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532704, ""price"": ""29183.21000000"", ""qty"": ""0.06190000"", ""quoteQty"": ""1806.44069900"", ""time"": 1690533541649, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532705, ""price"": ""29183.21000000"", ""qty"": ""0.00039000"", ""quoteQty"": ""11.38145190"", ""time"": 1690533541650, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532706, ""price"": ""29183.21000000"", ""qty"": ""0.06900000"", ""quoteQty"": ""2013.64149000"", ""time"": 1690533541650, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532707, ""price"": ""29183.21000000"", ""qty"": ""0.06604000"", ""quoteQty"": ""1927.25918840"", ""time"": 1690533541652, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532708, ""price"": ""29183.21000000"", ""qty"": ""0.00038000"", ""quoteQty"": ""11.08961980"", ""time"": 1690533541660, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532709, ""price"": ""29183.22000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.22981720"", ""time"": 1690533542101, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532710, ""price"": ""29183.22000000"", ""qty"": ""0.00524000"", ""quoteQty"": ""152.92007280"", ""time"": 1690533542502, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532711, ""price"": ""29183.21000000"", ""qty"": ""0.01292000"", ""quoteQty"": ""377.04707320"", ""time"": 1690533542518, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532712, ""price"": ""29183.21000000"", ""qty"": ""0.00376000"", ""quoteQty"": ""109.72886960"", ""time"": 1690533542840, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532713, ""price"": ""29183.22000000"", ""qty"": ""0.02466000"", ""quoteQty"": ""719.65820520"", ""time"": 1690533543073, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532714, ""price"": ""29183.21000000"", ""qty"": ""0.01051000"", ""quoteQty"": ""306.71553710"", ""time"": 1690533543250, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532715, ""price"": ""29183.21000000"", ""qty"": ""0.00301000"", ""quoteQty"": ""87.84146210"", ""time"": 1690533543980, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532716, ""price"": ""29183.22000000"", ""qty"": ""0.00126000"", ""quoteQty"": ""36.77085720"", ""time"": 1690533544788, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532717, ""price"": ""29183.22000000"", ""qty"": ""0.02467000"", ""quoteQty"": ""719.95003740"", ""time"": 1690533545073, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532718, ""price"": ""29183.22000000"", ""qty"": ""0.03667000"", ""quoteQty"": ""1070.14867740"", ""time"": 1690533545869, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532719, ""price"": ""29183.22000000"", ""qty"": ""0.02467000"", ""quoteQty"": ""719.95003740"", ""time"": 1690533546073, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532720, ""price"": ""29183.22000000"", ""qty"": ""0.00123000"", ""quoteQty"": ""35.89536060"", ""time"": 1690533547505, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532721, ""price"": ""29183.22000000"", ""qty"": ""0.00075000"", ""quoteQty"": ""21.88741500"", ""time"": 1690533547973, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532722, ""price"": ""29183.21000000"", ""qty"": ""0.01188000"", ""quoteQty"": ""346.69653480"", ""time"": 1690533548863, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532723, ""price"": ""29183.22000000"", ""qty"": ""0.00094000"", ""quoteQty"": ""27.43222680"", ""time"": 1690533549085, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532724, ""price"": ""29183.22000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59558280"", ""time"": 1690533549383, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532725, ""price"": ""29183.21000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13641490"", ""time"": 1690533550167, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532726, ""price"": ""29183.21000000"", ""qty"": ""0.00786000"", ""quoteQty"": ""229.38003060"", ""time"": 1690533550647, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532727, ""price"": ""29183.22000000"", ""qty"": ""0.00239000"", ""quoteQty"": ""69.74789580"", ""time"": 1690533550671, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532728, ""price"": ""29183.21000000"", ""qty"": ""0.00300000"", ""quoteQty"": ""87.54963000"", ""time"": 1690533551198, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532729, ""price"": ""29183.21000000"", ""qty"": ""0.00037000"", ""quoteQty"": ""10.79778770"", ""time"": 1690533551783, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532730, ""price"": ""29183.22000000"", ""qty"": ""0.00074000"", ""quoteQty"": ""21.59558280"", ""time"": 1690533553843, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532731, ""price"": ""29183.21000000"", ""qty"": ""0.00300000"", ""quoteQty"": ""87.54963000"", ""time"": 1690533554303, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532732, ""price"": ""29183.22000000"", ""qty"": ""0.00274000"", ""quoteQty"": ""79.96202280"", ""time"": 1690533555048, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532733, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533555050, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532734, ""price"": ""29183.21000000"", ""qty"": ""0.00072000"", ""quoteQty"": ""21.01191120"", ""time"": 1690533555057, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532735, ""price"": ""29183.21000000"", ""qty"": ""0.00899000"", ""quoteQty"": ""262.35705790"", ""time"": 1690533555115, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532736, ""price"": ""29183.22000000"", ""qty"": ""0.00925000"", ""quoteQty"": ""269.94478500"", ""time"": 1690533555886, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532737, ""price"": ""29183.21000000"", ""qty"": ""0.00043000"", ""quoteQty"": ""12.54878030"", ""time"": 1690533556064, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532738, ""price"": ""29183.22000000"", ""qty"": ""0.00075000"", ""quoteQty"": ""21.88741500"", ""time"": 1690533556105, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532739, ""price"": ""29183.21000000"", ""qty"": ""0.01210000"", ""quoteQty"": ""353.11684100"", ""time"": 1690533556244, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532740, ""price"": ""29183.22000000"", ""qty"": ""0.00444000"", ""quoteQty"": ""129.57349680"", ""time"": 1690533556351, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532741, ""price"": ""29183.22000000"", ""qty"": ""0.43748000"", ""quoteQty"": ""12767.07508560"", ""time"": 1690533556689, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532742, ""price"": ""29183.22000000"", ""qty"": ""0.16254000"", ""quoteQty"": ""4743.44057880"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532743, ""price"": ""29183.22000000"", ""qty"": ""0.44559000"", ""quoteQty"": ""13003.75099980"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532744, ""price"": ""29183.22000000"", ""qty"": ""0.07431000"", ""quoteQty"": ""2168.60507820"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532745, ""price"": ""29183.22000000"", ""qty"": ""0.23150000"", ""quoteQty"": ""6755.91543000"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532746, ""price"": ""29183.22000000"", ""qty"": ""0.00651000"", ""quoteQty"": ""189.98276220"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532747, ""price"": ""29183.22000000"", ""qty"": ""0.00636000"", ""quoteQty"": ""185.60527920"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532748, ""price"": ""29183.22000000"", ""qty"": ""0.30000000"", ""quoteQty"": ""8754.96600000"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532749, ""price"": ""29183.22000000"", ""qty"": ""0.18870000"", ""quoteQty"": ""5506.87361400"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532750, ""price"": ""29183.22000000"", ""qty"": ""0.36466000"", ""quoteQty"": ""10641.95300520"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532751, ""price"": ""29183.22000000"", ""qty"": ""1.34618000"", ""quoteQty"": ""39285.86709960"", ""time"": 1690533556690, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532752, ""price"": ""29183.22000000"", ""qty"": ""0.03701000"", ""quoteQty"": ""1080.07097220"", ""time"": 1690533556691, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532753, ""price"": ""29183.22000000"", ""qty"": ""0.06604000"", ""quoteQty"": ""1927.25984880"", ""time"": 1690533556860, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532754, ""price"": ""29183.21000000"", ""qty"": ""0.00769000"", ""quoteQty"": ""224.41888490"", ""time"": 1690533556866, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532755, ""price"": ""29183.22000000"", ""qty"": ""0.00038000"", ""quoteQty"": ""11.08962360"", ""time"": 1690533556868, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532756, ""price"": ""29183.22000000"", ""qty"": ""0.00039000"", ""quoteQty"": ""11.38145580"", ""time"": 1690533556869, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532757, ""price"": ""29183.22000000"", ""qty"": ""0.01966000"", ""quoteQty"": ""573.74210520"", ""time"": 1690533557136, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532758, ""price"": ""29183.22000000"", ""qty"": ""0.01253000"", ""quoteQty"": ""365.66574660"", ""time"": 1690533557660, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532759, ""price"": ""29183.22000000"", ""qty"": ""0.05673000"", ""quoteQty"": ""1655.56407060"", ""time"": 1690533557660, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532760, ""price"": ""29183.22000000"", ""qty"": ""0.00382000"", ""quoteQty"": ""111.47990040"", ""time"": 1690533557719, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532761, ""price"": ""29183.22000000"", ""qty"": ""0.00184000"", ""quoteQty"": ""53.69712480"", ""time"": 1690533558161, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532762, ""price"": ""29183.22000000"", ""qty"": ""0.00231000"", ""quoteQty"": ""67.41323820"", ""time"": 1690533558261, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532763, ""price"": ""29183.22000000"", ""qty"": ""0.00374000"", ""quoteQty"": ""109.14524280"", ""time"": 1690533558584, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532764, ""price"": ""29183.22000000"", ""qty"": ""0.00346000"", ""quoteQty"": ""100.97394120"", ""time"": 1690533559144, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532765, ""price"": ""29183.21000000"", ""qty"": ""0.00972000"", ""quoteQty"": ""283.66080120"", ""time"": 1690533560399, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532766, ""price"": ""29183.22000000"", ""qty"": ""0.00267000"", ""quoteQty"": ""77.91919740"", ""time"": 1690533560590, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532767, ""price"": ""29183.22000000"", ""qty"": ""0.00102000"", ""quoteQty"": ""29.76688440"", ""time"": 1690533560664, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532768, ""price"": ""29183.22000000"", ""qty"": ""0.00100000"", ""quoteQty"": ""29.18322000"", ""time"": 1690533561250, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532769, ""price"": ""29183.21000000"", ""qty"": ""0.00045000"", ""quoteQty"": ""13.13244450"", ""time"": 1690533561362, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532770, ""price"": ""29183.22000000"", ""qty"": ""0.00114000"", ""quoteQty"": ""33.26887080"", ""time"": 1690533561401, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532771, ""price"": ""29183.22000000"", ""qty"": ""0.01394000"", ""quoteQty"": ""406.81408680"", ""time"": 1690533561663, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532772, ""price"": ""29183.22000000"", ""qty"": ""0.00202000"", ""quoteQty"": ""58.95010440"", ""time"": 1690533561663, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532773, ""price"": ""29183.22000000"", ""qty"": ""0.05330000"", ""quoteQty"": ""1555.46562600"", ""time"": 1690533561663, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532774, ""price"": ""29183.22000000"", ""qty"": ""0.00045000"", ""quoteQty"": ""13.13244900"", ""time"": 1690533562324, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532775, ""price"": ""29183.22000000"", ""qty"": ""0.00137000"", ""quoteQty"": ""39.98101140"", ""time"": 1690533562444, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532776, ""price"": ""29183.21000000"", ""qty"": ""0.00060000"", ""quoteQty"": ""17.50992600"", ""time"": 1690533562475, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532777, ""price"": ""29183.22000000"", ""qty"": ""0.00184000"", ""quoteQty"": ""53.69712480"", ""time"": 1690533562516, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532778, ""price"": ""29183.22000000"", ""qty"": ""0.00152000"", ""quoteQty"": ""44.35849440"", ""time"": 1690533562721, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532779, ""price"": ""29183.22000000"", ""qty"": ""0.05262000"", ""quoteQty"": ""1535.62103640"", ""time"": 1690533563166, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532780, ""price"": ""29183.22000000"", ""qty"": ""0.01664000"", ""quoteQty"": ""485.60878080"", ""time"": 1690533563166, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532781, ""price"": ""29183.22000000"", ""qty"": ""0.00462000"", ""quoteQty"": ""134.82647640"", ""time"": 1690533564305, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532782, ""price"": ""29183.21000000"", ""qty"": ""0.00172000"", ""quoteQty"": ""50.19512120"", ""time"": 1690533564966, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532783, ""price"": ""29183.22000000"", ""qty"": ""0.00309000"", ""quoteQty"": ""90.17614980"", ""time"": 1690533565247, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532784, ""price"": ""29183.22000000"", ""qty"": ""0.00115000"", ""quoteQty"": ""33.56070300"", ""time"": 1690533566357, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532785, ""price"": ""29183.22000000"", ""qty"": ""0.00063000"", ""quoteQty"": ""18.38542860"", ""time"": 1690533566358, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532786, ""price"": ""29183.21000000"", ""qty"": ""0.00072000"", ""quoteQty"": ""21.01191120"", ""time"": 1690533566475, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532787, ""price"": ""29183.22000000"", ""qty"": ""0.00116000"", ""quoteQty"": ""33.85253520"", ""time"": 1690533566729, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532788, ""price"": ""29183.22000000"", ""qty"": ""0.00115000"", ""quoteQty"": ""33.56070300"", ""time"": 1690533567112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532789, ""price"": ""29183.22000000"", ""qty"": ""0.00084000"", ""quoteQty"": ""24.51390480"", ""time"": 1690533567126, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532790, ""price"": ""29183.22000000"", ""qty"": ""0.01055000"", ""quoteQty"": ""307.88297100"", ""time"": 1690533567197, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532791, ""price"": ""29183.22000000"", ""qty"": ""0.00115000"", ""quoteQty"": ""33.56070300"", ""time"": 1690533567496, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532792, ""price"": ""29183.21000000"", ""qty"": ""0.02242000"", ""quoteQty"": ""654.28756820"", ""time"": 1690533567816, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532793, ""price"": ""29183.22000000"", ""qty"": ""0.00410000"", ""quoteQty"": ""119.65120200"", ""time"": 1690533567922, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532794, ""price"": ""29183.22000000"", ""qty"": ""0.00137000"", ""quoteQty"": ""39.98101140"", ""time"": 1690533568225, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532795, ""price"": ""29183.21000000"", ""qty"": ""0.00525000"", ""quoteQty"": ""153.21185250"", ""time"": 1690533568567, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532796, ""price"": ""29183.22000000"", ""qty"": ""0.00075000"", ""quoteQty"": ""21.88741500"", ""time"": 1690533568706, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532797, ""price"": ""29183.22000000"", ""qty"": ""0.00508000"", ""quoteQty"": ""148.25075760"", ""time"": 1690533570156, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532798, ""price"": ""29183.21000000"", ""qty"": ""0.01006000"", ""quoteQty"": ""293.58309260"", ""time"": 1690533570264, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532799, ""price"": ""29183.21000000"", ""qty"": ""0.01000000"", ""quoteQty"": ""291.83210000"", ""time"": 1690533570567, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532800, ""price"": ""29183.22000000"", ""qty"": ""0.00154000"", ""quoteQty"": ""44.94215880"", ""time"": 1690533570836, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532801, ""price"": ""29183.22000000"", ""qty"": ""0.00037000"", ""quoteQty"": ""10.79779140"", ""time"": 1690533571369, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532802, ""price"": ""29183.22000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.22981720"", ""time"": 1690533571662, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532803, ""price"": ""29183.22000000"", ""qty"": ""0.00274000"", ""quoteQty"": ""79.96202280"", ""time"": 1690533572364, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532804, ""price"": ""29183.22000000"", ""qty"": ""0.00274000"", ""quoteQty"": ""79.96202280"", ""time"": 1690533572364, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532805, ""price"": ""29183.22000000"", ""qty"": ""0.00068000"", ""quoteQty"": ""19.84458960"", ""time"": 1690533572367, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532806, ""price"": ""29183.22000000"", ""qty"": ""0.00067000"", ""quoteQty"": ""19.55275740"", ""time"": 1690533572369, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532807, ""price"": ""29183.22000000"", ""qty"": ""0.04013000"", ""quoteQty"": ""1171.12261860"", ""time"": 1690533573384, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532808, ""price"": ""29183.22000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.22981720"", ""time"": 1690533574164, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532809, ""price"": ""29183.22000000"", ""qty"": ""0.00038000"", ""quoteQty"": ""11.08962360"", ""time"": 1690533574231, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532810, ""price"": ""29183.21000000"", ""qty"": ""0.00365000"", ""quoteQty"": ""106.51871650"", ""time"": 1690533574245, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532811, ""price"": ""29183.22000000"", ""qty"": ""0.00423000"", ""quoteQty"": ""123.44502060"", ""time"": 1690533574269, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532812, ""price"": ""29183.22000000"", ""qty"": ""0.00473000"", ""quoteQty"": ""138.03663060"", ""time"": 1690533574366, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532813, ""price"": ""29183.21000000"", ""qty"": ""0.00349000"", ""quoteQty"": ""101.84940290"", ""time"": 1690533576082, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532814, ""price"": ""29183.22000000"", ""qty"": ""0.00075000"", ""quoteQty"": ""21.88741500"", ""time"": 1690533576138, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532815, ""price"": ""29183.22000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.22981720"", ""time"": 1690533576167, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532816, ""price"": ""29183.22000000"", ""qty"": ""0.00234000"", ""quoteQty"": ""68.28873480"", ""time"": 1690533576564, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532817, ""price"": ""29183.22000000"", ""qty"": ""0.00078000"", ""quoteQty"": ""22.76291160"", ""time"": 1690533576669, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532818, ""price"": ""29183.22000000"", ""qty"": ""0.00149000"", ""quoteQty"": ""43.48299780"", ""time"": 1690533576737, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532819, ""price"": ""29183.22000000"", ""qty"": ""0.10628000"", ""quoteQty"": ""3101.59262160"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532820, ""price"": ""29183.22000000"", ""qty"": ""0.12295000"", ""quoteQty"": ""3588.07689900"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532821, ""price"": ""29183.22000000"", ""qty"": ""0.00189000"", ""quoteQty"": ""55.15628580"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532822, ""price"": ""29183.22000000"", ""qty"": ""0.00082000"", ""quoteQty"": ""23.93024040"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532823, ""price"": ""29183.22000000"", ""qty"": ""0.00792000"", ""quoteQty"": ""231.13110240"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532824, ""price"": ""29183.43000000"", ""qty"": ""0.06625000"", ""quoteQty"": ""1933.40223750"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532825, ""price"": ""29183.49000000"", ""qty"": ""0.01189000"", ""quoteQty"": ""346.99169610"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532826, ""price"": ""29183.49000000"", ""qty"": ""0.00573000"", ""quoteQty"": ""167.22139770"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532827, ""price"": ""29183.54000000"", ""qty"": ""0.00150000"", ""quoteQty"": ""43.77531000"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532828, ""price"": ""29183.55000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34684000"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532829, ""price"": ""29183.55000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26519500"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532830, ""price"": ""29183.58000000"", ""qty"": ""0.01164000"", ""quoteQty"": ""339.69687120"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532831, ""price"": ""29183.62000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26525800"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532832, ""price"": ""29183.62000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34689600"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532833, ""price"": ""29183.63000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26526700"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532834, ""price"": ""29183.95000000"", ""qty"": ""0.03427000"", ""quoteQty"": ""1000.13396650"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532835, ""price"": ""29184.14000000"", ""qty"": ""0.08000000"", ""quoteQty"": ""2334.73120000"", ""time"": 1690533577108, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532836, ""price"": ""29184.38000000"", ""qty"": ""0.00568000"", ""quoteQty"": ""165.76727840"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532837, ""price"": ""29184.43000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26598700"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532838, ""price"": ""29184.43000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34754400"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532839, ""price"": ""29184.50000000"", ""qty"": ""0.00080000"", ""quoteQty"": ""23.34760000"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532840, ""price"": ""29184.50000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26605000"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532841, ""price"": ""29184.51000000"", ""qty"": ""0.00090000"", ""quoteQty"": ""26.26605900"", ""time"": 1690533577112, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532842, ""price"": ""29184.67000000"", ""qty"": ""0.00119000"", ""quoteQty"": ""34.72975730"", ""time"": 1690533577117, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532843, ""price"": ""29184.67000000"", ""qty"": ""0.01040000"", ""quoteQty"": ""303.52056800"", ""time"": 1690533577121, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532844, ""price"": ""29184.68000000"", ""qty"": ""0.00051000"", ""quoteQty"": ""14.88418680"", ""time"": 1690533577239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532845, ""price"": ""29184.68000000"", ""qty"": ""0.00694000"", ""quoteQty"": ""202.54167920"", ""time"": 1690533577239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532846, ""price"": ""29184.68000000"", ""qty"": ""0.00669000"", ""quoteQty"": ""195.24550920"", ""time"": 1690533577239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532847, ""price"": ""29184.68000000"", ""qty"": ""0.02986000"", ""quoteQty"": ""871.45454480"", ""time"": 1690533577239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532848, ""price"": ""29184.68000000"", ""qty"": ""0.01245000"", ""quoteQty"": ""363.34926600"", ""time"": 1690533577289, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532849, ""price"": ""29184.67000000"", ""qty"": ""0.05480000"", ""quoteQty"": ""1599.31991600"", ""time"": 1690533577388, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532850, ""price"": ""29184.67000000"", ""qty"": ""0.00346000"", ""quoteQty"": ""100.97895820"", ""time"": 1690533577460, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532851, ""price"": ""29184.68000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13742920"", ""time"": 1690533577562, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532852, ""price"": ""29184.67000000"", ""qty"": ""0.00974000"", ""quoteQty"": ""284.25868580"", ""time"": 1690533577692, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532853, ""price"": ""29184.67000000"", ""qty"": ""0.01591000"", ""quoteQty"": ""464.32809970"", ""time"": 1690533577715, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532854, ""price"": ""29184.67000000"", ""qty"": ""0.08911000"", ""quoteQty"": ""2600.64594370"", ""time"": 1690533578444, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532855, ""price"": ""29184.68000000"", ""qty"": ""0.00137000"", ""quoteQty"": ""39.98301160"", ""time"": 1690533580208, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532856, ""price"": ""29184.67000000"", ""qty"": ""0.00476000"", ""quoteQty"": ""138.91902920"", ""time"": 1690533581569, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532857, ""price"": ""29184.67000000"", ""qty"": ""0.00410000"", ""quoteQty"": ""119.65714700"", ""time"": 1690533581731, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532858, ""price"": ""29184.67000000"", ""qty"": ""0.01995000"", ""quoteQty"": ""582.23416650"", ""time"": 1690533581734, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532859, ""price"": ""29184.67000000"", ""qty"": ""0.07000000"", ""quoteQty"": ""2042.92690000"", ""time"": 1690533581734, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532860, ""price"": ""29184.67000000"", ""qty"": ""0.11140000"", ""quoteQty"": ""3251.17223800"", ""time"": 1690533581734, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532861, ""price"": ""29184.67000000"", ""qty"": ""0.00552000"", ""quoteQty"": ""161.09937840"", ""time"": 1690533581734, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532862, ""price"": ""29184.68000000"", ""qty"": ""0.06043000"", ""quoteQty"": ""1763.63021240"", ""time"": 1690533581828, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532863, ""price"": ""29184.68000000"", ""qty"": ""1.48992000"", ""quoteQty"": ""43482.83842560"", ""time"": 1690533581836, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532864, ""price"": ""29184.68000000"", ""qty"": ""0.00173000"", ""quoteQty"": ""50.48949640"", ""time"": 1690533581847, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532865, ""price"": ""29184.68000000"", ""qty"": ""0.04400000"", ""quoteQty"": ""1284.12592000"", ""time"": 1690533581871, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532866, ""price"": ""29184.68000000"", ""qty"": ""0.01700000"", ""quoteQty"": ""496.13956000"", ""time"": 1690533581908, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532867, ""price"": ""29184.67000000"", ""qty"": ""0.00053000"", ""quoteQty"": ""15.46787510"", ""time"": 1690533581954, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532868, ""price"": ""29184.68000000"", ""qty"": ""0.00300000"", ""quoteQty"": ""87.55404000"", ""time"": 1690533581965, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532869, ""price"": ""29184.67000000"", ""qty"": ""0.00075000"", ""quoteQty"": ""21.88850250"", ""time"": 1690533582353, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532870, ""price"": ""29184.68000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.33093680"", ""time"": 1690533582747, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532871, ""price"": ""29184.68000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.33093680"", ""time"": 1690533583176, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532872, ""price"": ""29184.67000000"", ""qty"": ""0.00036000"", ""quoteQty"": ""10.50648120"", ""time"": 1690533583795, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532873, ""price"": ""29184.67000000"", ""qty"": ""0.00273000"", ""quoteQty"": ""79.67414910"", ""time"": 1690533584110, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532874, ""price"": ""29184.68000000"", ""qty"": ""0.00137000"", ""quoteQty"": ""39.98301160"", ""time"": 1690533584618, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532875, ""price"": ""29184.68000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.33093680"", ""time"": 1690533585190, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532876, ""price"": ""29184.67000000"", ""qty"": ""0.02055000"", ""quoteQty"": ""599.74496850"", ""time"": 1690533587064, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532877, ""price"": ""29184.68000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.33093680"", ""time"": 1690533587239, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532878, ""price"": ""29184.68000000"", ""qty"": ""0.06926000"", ""quoteQty"": ""2021.33093680"", ""time"": 1690533587732, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532879, ""price"": ""29184.67000000"", ""qty"": ""0.00412000"", ""quoteQty"": ""120.24084040"", ""time"": 1690533587878, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532880, ""price"": ""29184.67000000"", ""qty"": ""0.00175000"", ""quoteQty"": ""51.07317250"", ""time"": 1690533588003, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532881, ""price"": ""29184.68000000"", ""qty"": ""0.01985000"", ""quoteQty"": ""579.31589800"", ""time"": 1690533588638, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532882, ""price"": ""29184.67000000"", ""qty"": ""0.01274000"", ""quoteQty"": ""371.81269580"", ""time"": 1690533588766, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532883, ""price"": ""29184.67000000"", ""qty"": ""0.00172000"", ""quoteQty"": ""50.19763240"", ""time"": 1690533588782, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532884, ""price"": ""29184.68000000"", ""qty"": ""0.07771000"", ""quoteQty"": ""2267.94148280"", ""time"": 1690533589363, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532885, ""price"": ""29184.68000000"", ""qty"": ""0.00685000"", ""quoteQty"": ""199.91505800"", ""time"": 1690533589663, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532886, ""price"": ""29184.68000000"", ""qty"": ""0.00274000"", ""quoteQty"": ""79.96602320"", ""time"": 1690533590644, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532887, ""price"": ""29184.68000000"", ""qty"": ""0.00274000"", ""quoteQty"": ""79.96602320"", ""time"": 1690533590645, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532888, ""price"": ""29184.68000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13742920"", ""time"": 1690533590647, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532889, ""price"": ""29184.68000000"", ""qty"": ""0.00069000"", ""quoteQty"": ""20.13742920"", ""time"": 1690533590647, ""isBuyerMaker"": false, ""isBestMatch"": true }, { ""id"": 3180532890, ""price"": ""29184.67000000"", ""qty"": ""0.01273000"", ""quoteQty"": ""371.52084910"", ""time"": 1690533590767, ""isBuyerMaker"": true, ""isBestMatch"": true }, { ""id"": 3180532891, ""price"": ""29184.67000000"", ""qty"": ""0.01273000"", ""quoteQty"": ""371.52084910"", ""time"": 1690533591767, ""isBuyerMaker"": true, ""isBestMatch"": true } ]";
        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var jsonNode, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetTrade, ResultCode.GetTradeFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);

        var trades = Parse(jsonNode, security);
        return ExternalQueryStates.QueryTrades(content, connId, security.Code, trades).RecordTimes(rtt, swOuter);


        static List<Trade> Parse(JsonNode json, Security security)
        {
            var rootArray = json.AsArray();
            var trades = new List<Trade>(rootArray.Count);

            for (int i = 0; i < rootArray.Count; i++)
            {
                JsonNode? node = rootArray[i];
                try
                {
                    if (node == null) throw new NullReferenceException("Empty trade node.");
                    var tradeObj = node.AsObject();
                    var tradeId = tradeObj.GetLong("id");
                    var price = tradeObj.GetDecimal("price");
                    var quantity = tradeObj.GetDecimal("qty");
                    //var notional = tradeObj["quoteQty"].GetDecimal();
                    var time = tradeObj.GetUtcFromUnixMs("time");
                    var side = tradeObj.GetBoolean("isBuyerMaker") ? Side.Sell : Side.Buy;
                    var trade = new Trade
                    {
                        Id = 0,
                        AccountId = 0,
                        ExternalOrderId = 0,
                        Fee = 0,
                        FeeAssetId = 0,
                        FeeAssetCode = "",
                        OrderId = 0,
                        SecurityId = security.Id,
                        SecurityCode = "",
                        ExternalTradeId = tradeId,
                        Price = price,
                        Quantity = quantity,
                        Time = time,
                        Side = side,
                    };
                    trades.Add(trade);
                }
                catch (Exception e)
                {
                    // silently ignore
                    _log.Error("Failed to parse a trade json node.", e);
                }
            }
            return trades;
        }
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
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (!ValidateSecurity(security, ActionType.GetOrder, out var errorState))
            return errorState;

        if (orderId <= 0 && externalOrderId <= 0)
        {
            return ExternalQueryStates.InvalidArgument(ActionType.GetOrder, "Must provide either order id or external order id.");
        }

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/order";
        var parameters = new List<(string, string)> { ("symbol", security.Code) };
        if (orderId > 0)
            parameters.Add(("externalOrderId", orderId.ToString()));
        else
            parameters.Add(("origClientOrderId", externalOrderId.ToString()));
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url, parameters);
        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        // example JSON: var content = @"{ ""symbol"": ""LTCBTC"", ""externalOrderId"": 1, ""orderListId"": -1, ""clientOrderId"": ""myOrder1"", ""price"": ""0.1"", ""origQty"": ""1.0"", ""executedQty"": ""0.0"", ""cummulativeQuoteQty"": ""0.0"", ""status"": ""NEW"", ""timeInForce"": ""GTC"", ""type"": ""LIMIT"", ""side"": ""BUY"", ""stopPrice"": ""0.0"", ""icebergQty"": ""0.0"", ""time"": 1499827319559, ""updateTime"": 1499827319559, ""isWorking"": true, ""workingTime"":1499827319559, ""origQuoteOrderQty"": ""0.000000"", ""selfTradePreventionMode"": ""NONE"" }";
        if (!response.ParseJsonObject(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetOrder, ResultCode.GetOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);
        var order = ParseOrder(json, security.Id);
        return ExternalQueryStates.QueryOrder(order, content, connId).RecordTimes(rtt, swOuter);
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
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (security != null && !ValidateSecurity(security, ActionType.GetOrder, out var errorState))
            return errorState;

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/openOrders";
        var parameters = new List<(string, string)>(1);
        if (security != null)
        {
            parameters.Add(("symbol", security.Code));
        }
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        // example JSON: var content = @"{ ""symbol"": ""LTCBTC"", ""externalOrderId"": 1, ""orderListId"": -1, ""clientOrderId"": ""myOrder1"", ""price"": ""0.1"", ""origQty"": ""1.0"", ""executedQty"": ""0.0"", ""cummulativeQuoteQty"": ""0.0"", ""status"": ""NEW"", ""timeInForce"": ""GTC"", ""type"": ""LIMIT"", ""side"": ""BUY"", ""stopPrice"": ""0.0"", ""icebergQty"": ""0.0"", ""time"": 1499827319559, ""updateTime"": 1499827319559, ""isWorking"": true, ""workingTime"":1499827319559, ""origQuoteOrderQty"": ""0.000000"", ""selfTradePreventionMode"": ""NONE"" }";
        if (!response.ParseJsonArray(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetOrder, ResultCode.GetOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);
        var orders = ParseOrders(json, security);
        return ExternalQueryStates.QueryOrders(security?.Code, content, connId, orders: orders).RecordTimes(rtt, swOuter);
    }

    /// <summary>
    /// Get orders given a security and time period [SIGNED].
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOrders(Security security,
                                                    long oldestExternalOrderId = long.MinValue,
                                                    DateTime? start = null,
                                                    DateTime? end = null)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (!ValidateSecurity(security, ActionType.GetOrder, out var errorState))
            return errorState;

        if (start == null && end != null)
            return ExternalQueryStates.InvalidArgument(ActionType.GetOrder, "If end time is provided, start time must also be provided.");

        if (start != null && end != null && end <= start)
            return ExternalQueryStates.InvalidArgument(ActionType.GetOrder, "Start time must be smaller than end time.");

        if (end != null && start != null && end - start > TimeSpans.OneDay)
        {
            // Binance allows 24h retrieval only
            var results = new List<Order>();
            var states = new List<ExternalQueryState>();
            var totalRtt = 0L;
            var totalTime = 0L;
            foreach (var (s, e) in (start.Value, end.Value).Split(TimeSpans.OneDay))
            {
                var state = await GetOrders(security, oldestExternalOrderId, s, e);
                results.AddOrAddRange(state.Get<List<Order>>(), state.Get<Order>());
                totalRtt += state.NetworkRoundtripTime;
                totalTime += state.TotalTime;
                states.Add(state);
            }
            var totalState = ExternalQueryStates.QueryOrders("", "", security.Code, results).RecordTimes(totalRtt, totalTime);
            totalState.SubStates = states;
            return totalState;
        }

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/allOrders";
        var parameters = new List<(string, string)>();
        if (security != null)
        {
            parameters.Add(("symbol", security.Code));
        }
        if (oldestExternalOrderId.IsValid())
        {
            parameters.Add(("orderId", oldestExternalOrderId.ToString()));
        }
        if (start != null)
        {
            var startMs = start.Value.ToUnixMs();
            parameters.Add(("startTime", startMs.ToString()));
            if (end != null)
            {
                var endMs = end.Value.ToUnixMs();
                parameters.Add(("endTime", endMs.ToString()));
            }
        }
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonArray(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetOrder, ResultCode.GetOrderFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage);

        var orders = ParseOrders(json, security);
        return ExternalQueryStates.QueryOrders(security?.Code, content, connId, orders).RecordTimes(rtt, swOuter);
    }

    /// <summary>
    /// Update an order [SIGNED].
    /// Binance only supports cancel + replace order action.
    /// </summary>
    /// <param name="updatedOrder"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> UpdateOrder(Order updatedOrder)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        // TODO Not Tested
        var toCancel = updatedOrder with { };
        var cancelState = await CancelOrder(toCancel);
        var sendState = await SendOrder(updatedOrder);
        return ExternalQueryStates.UpdateOrder(toCancel, updatedOrder, cancelState, sendState);
    }

    /// <summary>
    /// Get the speed limit for sending orders [SIGNED]
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetOrderSpeedLimit()
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/rateLimit/order";
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        return !response.ParseJsonArray(out var content, out var json, out var errorMessage)
            ? ExternalQueryStates.Error(ActionType.GetFrequencyRestriction, ResultCode.GetMiscFailed, Errors.ProcessErrorMessage(errorMessage), content, connId, errorMessage)
            : new ExternalQueryState
            {
                Content = Parse(json),
                ResponsePayload = content,
                Action = ActionType.GetFrequencyRestriction,
                ExternalId = ExternalQueryStates.BrokerId,
                ResultCode = ResultCode.GetMiscOk,
                UniqueConnectionId = connId,
                Description = "Get order speed limit",
            }.RecordTimes(rtt, swOuter);

        static RequestFrequencyInfo[] Parse(JsonArray json)
        {
            var infoArray = new RequestFrequencyInfo[json.Count];
            for (int i = 0; i < json.Count; i++)
            {
                JsonNode? node = json[i];
                var obj = node?.AsObject();
                if (obj == null) continue;

                RequestFrequencyInfo? info = null;
                try
                {
                    info = new RequestFrequencyInfo(
                        json.GetString("rateLimitType"),
                        IntervalTypeConverter.Parse(json.GetString("interval")),
                        json.GetInt("intervalNum"),
                        json.GetInt("count"),
                        json.GetInt("limit")
                    );
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to parse trade json.", ex);
                }

                infoArray[i] = info;
            }
            return infoArray;
        }
    }

    /// <summary>
    /// Get trades for a specific symbol [SIGNED].
    /// Valid combination with precedence:
    /// 1. symbol
    /// 2. symbol + externalOrderId
    /// 3. symbol + start (limit to 500 entries)
    /// 4. symbol + start + end (limit to 500 entries)
    /// </summary>
    /// <param name="security"></param>
    /// <param name="externalOrderId"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetTrades(Security security,
                                                    long externalOrderId = long.MinValue,
                                                    DateTime? start = null,
                                                    DateTime? end = null)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (!ValidateSecurity(security, ActionType.GetTrade, out var errorState))
            return errorState!;

        if (!externalOrderId.IsValid() && start == null && end == null)
            return ExternalQueryStates.InvalidArgument(ActionType.GetTrade, "Must provide at least an orderId, start or end time.");

        if (start == null && end != null)
            return ExternalQueryStates.InvalidArgument(ActionType.GetTrade, "If end time is provided, start time must also be provided.");

        if (start != null && end != null && end <= start)
            return ExternalQueryStates.InvalidArgument(ActionType.GetTrade, "Start time must be smaller than end time.");

        end ??= DateTime.UtcNow;
        if (end != null && start != null && end - start > TimeSpans.OneDay)
        {
            // Binance allows 24h retrieval only
            var results = new List<Trade>();
            var states = new List<ExternalQueryState>();
            var totalRtt = 0L;
            var totalTime = 0L;
            foreach (var (s, e) in (start.Value, end.Value).Split(TimeSpans.OneDay))
            {
                var state = await GetTrades(security, externalOrderId, s, e);
                results.AddOrAddRange(state.Get<List<Trade>>(), state.Get<Trade>());
                totalRtt += state.NetworkRoundtripTime;
                totalTime += state.TotalTime;
                states.Add(state);
            }
            var totalState = ExternalQueryStates.QueryTrades("", "", security.Code, results).RecordTimes(totalRtt, totalTime);
            totalState.SubStates = states;
            return totalState;
        }

        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/myTrades";
        var parameters = new List<(string, string)>
        {
            ("symbol", security.Code),
        };
        if (externalOrderId.IsValid())
        {
            parameters.Add(("orderId", externalOrderId.ToString()));
        }
        if (start != null)
        {
            var startMs = start.Value.ToUnixMs();
            parameters.Add(("startTime", startMs.ToString()));
            if (end != null)
            {
                var endMs = end.Value.ToUnixMs();
                parameters.Add(("endTime", endMs.ToString()));
            }
        }
        parameters.Add(("limit", "500"));

        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonArray(out var content, out var json, out var errorMessage, _log))
        {
            var subCode = Errors.ProcessErrorMessage(errorMessage);
            return ExternalQueryStates.Error(ActionType.GetTrade, ResultCode.GetTradeFailed, subCode, content, connId, errorMessage);
        }
        var trades = ParseTrades(json, security);
        return ExternalQueryStates.QueryTrades(content, connId, security.Code, trades).RecordTimes(rtt, swOuter);
    }


    /// <summary>
    /// Get the assets via account api [SIGNED].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAssetPositions(string accountId)
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

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
        var assets = new List<Asset>();
        var balanceArray = json["balances"]?.AsArray();
        var updateTime = json.GetUtcFromUnixMs("updateTime");
        if (balanceArray != null)
        {
            foreach (var balanceObj in balanceArray)
            {
                if (balanceObj == null) continue;
                var code = balanceObj.GetString("asset");
                var free = balanceObj.GetDecimal("free");
                var locked = balanceObj.GetDecimal("locked");
                var asset = new Asset
                {
                    SecurityCode = code,
                    Quantity = free,
                    LockedQuantity = locked,
                    CreateTime = DateTime.MinValue,
                    UpdateTime = updateTime,
                };
                assets.Add(asset);
            }
            // guarantee ordering
            assets.Sort(Sorters.CodeSorter);
        }
        return ExternalQueryStates.QueryAssets(content, connId, assets).RecordTimes(rtt, swOuter);
    }

    /// <summary>
    /// Subscribe to current user data steam, including updates of account, asset, order and trade. [USER-STREAM]
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalConnectionState> Subscribe()
    {
        if (!Firewall.CanCall)
            return ExternalConnectionStates.FirewallBlocked();

        var url = $"{_connectivity.RootUrl}/api/v3/userDataStream";
        using var request = _requestBuilder.BuildApiKey(HttpMethod.Post, url);
        try
        {
            var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
            if (!response.ParseJsonObject(out var content, out var jsonNode, out var errorMessage, _log))
            {
                _log.Error(errorMessage);
                return ExternalConnectionStates.SubscriptionFailed(SubscriptionType.RealTimeExecutionData, errorMessage);
            }

            // mark the retrieved listen-key, and automatically ping it for aliveness
            _listenKey = jsonNode.GetString("listenKey");
            _listenKeyTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

            // subscribe to the listen key in order to listen to order/trade/account changes
            Uri uri = new($"{_connectivity.RootWebSocketUrl}/stream?streams={_listenKey}");

            string message = "";
            var ws = new ExtendedWebSocket(_log);
            ws.Listen(uri, OnStreamingDataReceived, OnWebSocketCreated);
            Threads.WaitUntil(() => _webSockets.ThreadSafeContains(_listenKey));

            return ExternalConnectionStates.Subscribed(SubscriptionType.RealTimeExecutionData, message);


            void OnWebSocketCreated()
            {
                message = $"Subscribed to user streaming data, listen-key: {_listenKey}";
                _log.Info(message);
                _webSockets.ThreadSafeSet(_listenKey, ws);
            }
        }
        catch (Exception e)
        {
            _log.Error("Failed to subscribe to data feed, message: " + e.Message, e);
            return ExternalConnectionStates.SubscriptionFailed(SubscriptionType.RealTimeExecutionData, "Failed to subscribe.");
        }
    }

    public async Task<ExternalQueryState> Unsubscribe()
    {
        if (!Firewall.CanCall)
            return ExternalQueryStates.FirewallBlocked();

        if (_listenKey == null || _listenKeyTimer == null)
            return ExternalQueryStates.Error(ActionType.Unsubscribe, ResultCode.Failed, ResultCode.Unknown, null, "", "Failed to remove subscription key: " + _listenKey);

        _listenKeyTimer.Dispose();

        var url = $"{_connectivity.RootUrl}/api/v3/userDataStream";
        using var request = _requestBuilder.Build(HttpMethod.Delete, url, new List<(string, string)> { ("listenKey", _listenKey) });
        var response = await _httpClient.SendAsync(request);
        if (!response.ParseJsonObject(out _, out _, out _))
        {
            _log.Error($"Delete listen-key {_listenKey} failed.");
            return ExternalQueryStates.Error(ActionType.Unsubscribe, ResultCode.Failed, ResultCode.Unknown, null, "", "Failed to remove subscription key: " + _listenKey);
        }
        return ExternalQueryStates.RemoveSubscriptionKey(_listenKey);
    }

    private static bool ValidateSecurity(Security? security, ActionType actionType, [NotNullWhen(false)] out ExternalQueryState? errorState)
    {
        errorState = null;
        if (security == null)
        {
            errorState = ExternalQueryStates.InvalidSecurity(actionType);
            return false;
        }
        if (!security.IsFrom(ExternalNames.Binance))
        {
            errorState = ExternalQueryStates.InvalidExchange(actionType, ExternalNames.Binance);
            return false;
        }
        return true;
    }

    private static List<Order> ParseOrders(JsonArray json, Security? security)
    {
        var orders = new List<Order>(json.Count);
        for (int i = 0; i < json.Count; i++)
        {
            JsonNode? node = json[i];
            var obj = node?.AsObject();
            if (obj == null)
                continue;
            var order = ParseOrder(obj, security?.Id);
            if (order == null)
                continue;
            orders.Add(order);
        }
        return orders;
    }

    private static Order ParseOrder(JsonObject rootObj, int? securityId)
    {
        // 'workingTime' is similar to 'time', usually smaller than 'updateTime'
        var createTime = rootObj.GetUtcFromUnixMs("time");
        var updateTime = rootObj.GetUtcFromUnixMs("updateTime");
        var order = new Order
        {
            Id = rootObj.GetLong("clientOrderId"),
            ExternalOrderId = rootObj.GetLong("id", rootObj.GetLong("orderId")),
            SecurityId = securityId ?? 0,
            SecurityCode = rootObj.GetString("symbol"),
            CreateTime = createTime,
            ExternalCreateTime = createTime,
            UpdateTime = updateTime,
            ExternalUpdateTime = updateTime,
            Price = rootObj.GetDecimal("price"),
            Quantity = rootObj.GetDecimal("origQty"),
            FilledQuantity = rootObj.GetDecimal("executedQty"),
            Status = OrderStatuses.ParseBinance(rootObj.GetString("status")),
            TimeInForce = rootObj.GetString("timeInForce").ConvertDescriptionToEnum(TimeInForceType.Unknown),
            Type = rootObj.GetString("type").ConvertDescriptionToEnum(OrderType.Unknown),
            Side = rootObj.GetString("side").ConvertDescriptionToEnum<Side>(),
            StopPrice = rootObj.GetDecimal("stopPrice"),
            // iceberg quantity is not supported yet
        };
        if (order.Type == OrderType.Unknown)
        {
            _log.Error($"Received an order without order type defined, EOID: {order.ExternalOrderId}, OID: {order.Id}");
        }
        else if (order.Type.IsLimit())
        {
            order.LimitPrice = order.Price;
            order.Price = 0;
        }
        return order;
    }

    private Trade? ParseTrade(JsonObject? rootObj, int? securityId)
    {
        if (rootObj == null) return null;
        try
        {
            return new Trade
            {
                Id = _tradeIdGenerator.NewTimeBasedId,
                ExternalOrderId = rootObj.GetLong("orderId"), // binance's order id is our external order id
                ExternalTradeId = rootObj.GetLong("id"),
                SecurityCode = rootObj.GetString("symbol"),
                Time = rootObj.GetUtcFromUnixMs("time"),
                Price = rootObj.GetDecimal("price"),
                Quantity = rootObj.GetDecimal("qty"),
                Fee = rootObj.GetDecimal("commission"),
                FeeAssetCode = rootObj.GetString("commissionAsset"),
                Side = rootObj.GetBoolean("isBuyer") ? Side.Buy : Side.Sell,
                // rootObj.GetBoolean("isBestMatch") ? 1 : -1,
                AccountId = _context.AccountId,

                // the trades got from external are lacking some ids
                // set it to temp to allow later logic to fix them
                SecurityId = securityId ?? 0,
                OrderId = 0,
                FeeAssetId = 0
            };
        }
        catch (Exception ex)
        {
            _log.Error("Failed to parse trade json.", ex);
            return null;
        }
    }

    private List<Trade> ParseTrades(JsonArray json, Security? security)
    {
        var trades = new List<Trade>(json.Count);
        for (int i = 0; i < json.Count; i++)
        {
            JsonNode? node = json[i];
            var obj = node?.AsObject();
            if (obj == null) continue;

            var trade = ParseTrade(obj, security?.Id);
            if (trade == null) continue;

            trades.Add(trade);
        }
        return trades;
    }

    /// <summary>
    /// Parse user's streaming data.
    /// </summary>
    /// <param name="bytes"></param>
    private void OnStreamingDataReceived(byte[] bytes)
    {
        string json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

        try
        {
            // expect { "stream": "...", "data": {...}}
            var jsonNode = JsonNode.Parse(json)?.AsObject()?["data"];
            if (jsonNode == null)
            {
                _log.Warn($"Unknown user-streaming data.");
                return;
            }
            var messageType = jsonNode.GetString("e");
            var eventTime = jsonNode.GetUtcFromUnixMs("E");
            switch (messageType)
            {
                case "outboundAccountPosition": // asset changes due to trading activities
                    {
                        var accountUpdateTime = jsonNode.GetUtcFromUnixMs("u");
                        var balanceArray = jsonNode["B"]?.AsArray();
                        if (balanceArray == null)
                        {
                            _log.Warn($"Unknown user-streaming account asset data."); break;
                        }
                        var assets = new List<Asset>();
                        foreach (var balanceObject in balanceArray.OfType<JsonObject>())
                        {
                            var asset = new Asset
                            {
                                UpdateTime = accountUpdateTime,
                                SecurityCode = balanceObject.GetString("a"),
                                Quantity = balanceObject.GetDecimal("f"),
                                LockedQuantity = balanceObject.GetDecimal("l"),
                            };
                            assets.Add(asset);
                        }
                        AssetsChanged?.Invoke(assets);
                    }
                    break;
                case "balanceUpdate": // asset changes due to deposit or withdrawal or fund transfer
                    {
                        var delta = jsonNode.GetDecimal("d");
                        var transaction = new TransferAction
                        {
                            Action = delta > 0 ? ActionType.Deposit : ActionType.Withdraw,
                            Quantity = Math.Abs(delta),
                            AssetCode = jsonNode.GetString("a"),
                            RequestTime = eventTime,
                            EffectiveTime = jsonNode.GetUtcFromUnixMs("T"),
                        };
                        Transferred?.Invoke(transaction);
                    }
                    break;
                case "executionReport": // order
                    if (_log.IsDebugEnabled)
                        _log.Debug("Received streamed JSON for execution:" + Environment.NewLine + json);

                    var sideString = jsonNode.GetString("S");
                    var code = jsonNode.GetString("s");
                    var orderId = jsonNode.GetLong("c");
                    var side = sideString == "BUY" ? Side.Buy : sideString == "SELL" ? Side.Sell : Side.None;
                    var orderType = jsonNode.GetString("o").ConvertDescriptionToEnum(OrderType.Unknown);
                    var timeInForce = jsonNode.GetString("f").ConvertDescriptionToEnum(TimeInForceType.Unknown);
                    var orderQuantity = jsonNode.GetDecimal("q");
                    var orderPrice = jsonNode.GetDecimal("p");
                    var stopPrice = jsonNode.GetDecimal("P");
                    var icebergQuantity = jsonNode.GetDecimal("F");
                    var orderListId = jsonNode.GetInt("g");
                    var cancellingOrderId = jsonNode.GetString("C");
                    var executionType = jsonNode.GetString("x");
                    var status = jsonNode.GetString("X");
                    var rejectionCode = jsonNode.GetString("r");
                    var externalOrderId = jsonNode.GetLong("i");
                    var lastExecutedQuantity = jsonNode.GetDecimal("l");
                    var cumulativeFilledQuantity = jsonNode.GetDecimal("z");
                    var lastExecutedPrice = jsonNode.GetDecimal("L");
                    var fee = jsonNode.GetDecimal("n");
                    var feeAssetCode = jsonNode.GetString("N");
                    var transactionTime = jsonNode.GetUtcFromUnixMs("T"); // transaction time
                    var externalTradeId = jsonNode.GetLong("t");
                    var isOnOrderBook = jsonNode.GetBoolean("w");
                    var isMakerOrder = jsonNode.GetBoolean("m");
                    var createTime = jsonNode.GetUtcFromUnixMs("O"); // order creation time
                    var cumulativeQuoteAssetTransactedNotional = jsonNode.GetDecimal("Z"); // SUM(prx*qty)
                    var lastQuoteAssetTransactedNotional = jsonNode.GetDecimal("Y"); // SUM(lastPrx*lastQty)
                    var quoteOrderQuantity = jsonNode.GetDecimal("Q");
                    //var selfTradePreventionMode = jsonNode.GetString("V"); // appears if the order is on order book
                    var trailingDelta = jsonNode.GetDecimal("d");
                    var trailingTime = jsonNode.GetUtcFromUnixMs("D");
                    var strategyId = jsonNode.GetInt("j");
                    strategyId = strategyId == int.MinValue ? Consts.DefaultStrategyId : strategyId;
                    var strategyType = jsonNode.GetInt("J");
                    var workingTime = jsonNode.GetUtcFromUnixMs("W"); // appears if the order is on order book
                    var isUsingSor = jsonNode.GetBoolean("uS");

                    var updateTime = DateUtils.Max(transactionTime, workingTime);

                    var order = new Order
                    {
                        Id = orderId, // need to be filled later
                        AccountId = _context.AccountId,
                        CreateTime = DateTime.MinValue, // need to be filled later
                        ExternalCreateTime = createTime,
                        ExternalUpdateTime = updateTime, // the transaction time which should also be order's update time
                        ExternalOrderId = externalOrderId,
                        UpdateTime = updateTime, // if no user changes, the update time should be the same as external update time
                        FilledQuantity = cumulativeFilledQuantity,
                        Status = OrderStatuses.ParseBinance(status),
                        Type = orderType,
                        TimeInForce = timeInForce,
                        Price = orderPrice,
                        Quantity = orderQuantity,
                        SecurityCode = code,
                        SecurityId = 0, // need to be filled later
                        Side = side,
                        StopPrice = stopPrice,
                        StrategyId = strategyId,
                    };
                    if (order.Status == OrderStatus.Unknown)
                    {
                        _log.Warn($"Received an order without order status defined, EOID: {order.ExternalOrderId}, OID: {order.Id}");
                    }
                    if (order.Type == OrderType.Unknown)
                    {
                        _log.Error($"Received an order without order type defined, EOID: {order.ExternalOrderId}, OID: {order.Id}");
                    }
                    else if (order.Type.IsLimit())
                    {
                        order.LimitPrice = orderPrice;
                        order.Price = 0;
                    }

                    Trade? trade = null;
                    // if a trade is generated
                    if (externalTradeId > 0)
                    {
                        trade = new Trade
                        {
                            Id = _tradeIdGenerator.NewTimeBasedId,
                            ExternalOrderId = externalOrderId,
                            SecurityId = 0, // need to be filled later
                            SecurityCode = code,
                            Side = side,
                            AccountId = _context.AccountId,
                            ExternalTradeId = externalTradeId,
                            Fee = fee,
                            FeeAssetCode = feeAssetCode,
                            FeeAssetId = 0, // need to be filled later
                            OrderId = 0, // need to be filled later
                            Price = lastExecutedPrice,
                            Quantity = lastExecutedQuantity,
                            Time = updateTime,
                            PositionId = 0, // need to be filled later
                        };
                    }

                    _broker.Enqueue(new EventInvokerTask(() =>
                    {
                        OrderReceived?.Invoke(order);
                        if (trade != null && !_processedExternalTradeIds.ThreadSafeContainsElseAdd(trade.ExternalTradeId))
                            TradeReceived?.Invoke(trade);
                    }));

                    break;
                default:
                    _log.Warn($"Unknown user-streaming data, type: {messageType}.");
                    break;
            }
        }
        catch (Exception e)
        {
            _log.Error($"Cannot parse user-streaming data.", e);
        }
    }

    private void SendHeartbeat(object? state)
    {
        if (_listenKey == null) return;

        var url = $"{_connectivity.RootUrl}/api/v3/userDataStream";
        using var request = _requestBuilder.BuildApiKey(HttpMethod.Put, url, new List<(string, string)> { ("listenKey", _listenKey) });
        try
        {
            var response = _httpClient.Send(request);
            if (!response.ParseJsonObject(out _, out _, out _))
                _log.Error($"Heartbeat request for listen-key {_listenKey} cannot be sent. CurrentTime: {DateTime.UtcNow:yyyyMMdd-HHmmss}");
        }
        catch (TimeoutException te)
        {
            _log.Error($"Heartbeat request for listen-key {_listenKey} timed out.", te);
        }
        catch (HttpRequestException hre)
        {
            _log.Error($"Failed to send/receive heartbeat for listen-key {_listenKey}.", hre);
        }
    }

    private class EventInvokerTask
    {
        private readonly Action _action;

        public EventInvokerTask(Action action)
        {
            _action = action;
        }

        public void Run()
        {
            _action.Invoke();
        }
    }
}
