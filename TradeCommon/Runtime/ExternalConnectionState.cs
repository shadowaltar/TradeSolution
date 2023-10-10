using Azure;
using BenchmarkDotNet.Running;
using Common;
using Iced.Intel;
using OfficeOpenXml.Style;
using System.Diagnostics;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Runtime;

public record ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ActionType Action { get; set; }
    public ResultCode ResultCode { get; set; }
    public int ExternalId { get; set; } = 0;
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }
    public List<ExternalConnectionState>? SubStates { get; set; }
    public override string ToString()
    {
        return $"ConnectState [{Type}] action [{Action}] [{ResultCode}][{UniqueConnectionId}]";
    }
}

public record ExternalQueryState : INetworkTimeState
{
    public object? Content { get; set; }

    public string? ResponsePayload { get; set; }

    public ActionType Action { get; set; }

    public ResultCode ResultCode { get; set; }

    public int ExternalId { get; set; } = 0;

    public string? UniqueConnectionId { get; set; }

    public string? Description { get; set; }

    public long NetworkRoundtripTime { get; set; }

    public long TotalTime { get; set; }

    public T? Get<T>() => Content is T typed ? typed : default;

    public List<T>? GetAll<T>()
    {
        if (Content is T typed)
        {
            return new List<T> { typed };
        }
        if (Content is List<T> list)
        {
            return list;
        }
        return null;
    }

    public List<ExternalQueryState>? SubStates { get; set; }

    public ExternalQueryState RecordTimes(long rtt, Stopwatch swOuter)
    {
        swOuter.Stop();
        NetworkRoundtripTime = rtt;
        TotalTime = swOuter.ElapsedMilliseconds;
        return this;
    }

    public ExternalQueryState RecordTimes(Stopwatch swOuter)
    {
        swOuter.Stop();
        NetworkRoundtripTime = -1;
        TotalTime = swOuter.ElapsedMilliseconds;
        return this;
    }

    public ExternalQueryState RecordTimes(long rtt)
    {
        NetworkRoundtripTime = rtt;
        TotalTime = -1;
        return this;
    }

    public ExternalQueryState RecordTimes(long rtt, long total)
    {
        NetworkRoundtripTime = rtt;
        TotalTime = total;
        return this;
    }

    public override string ToString()
    {
        return $"QueryState Time[{NetworkRoundtripTime}ms/{TotalTime}ms] Action[{Action}] [{ResultCode}] [{UniqueConnectionId}]";
    }

    public ExternalQueryState SetDescription(string description)
    {
        Description = description;
        return this;
    }
}

public interface INetworkTimeState
{
    long NetworkRoundtripTime { get; set; }
    long TotalTime { get; set; }
}

public enum SubscriptionType
{
    Unknown,
    QuotationService,
    MarketData,
    RealTimeMarketData,
    HistoricalMarketData,
    RealTimeExecutionData,
}

public enum ActionType
{
    Unknown,
    Connect,
    Disconnect,
    Subscribe,
    Unsubscribe,

    SendOrder,
    CancelOrder,
    UpdateOrder,

    GetAccount,
    GetTrade,
    GetOrder,
    GetPosition,

    GetMisc,
    GetFrequencyRestriction,

    Deposit,
    Withdraw,
}


public static class ExternalQueryStates
{
    public static EnvironmentType Environment { get; set; }
    public static ExchangeType Exchange { get; set; }
    public static int EnvironmentId { get; set; }
    public static BrokerType Broker { get; set; }
    public static int BrokerId { get; set; }

    public static ExternalQueryState InvalidSecurity(ActionType action)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalId = BrokerId,
            ResultCode = ResultCode.InvalidArgument,
            Description = $"Invalid or missing security.",
        };
    }

    public static ExternalQueryState InvalidArgument(ActionType action, string message)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalId = BrokerId,
            ResultCode = ResultCode.InvalidArgument,
            Description = message,
        };
    }

    public static ExternalQueryState InvalidExchange(ActionType action, string externalName)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalId = BrokerId,
            ResultCode = ResultCode.InvalidArgument,
            Description = $"Wrong exchange for the security; expecting {externalName}.",
        };
    }

    public static ExternalQueryState InvalidOrder(string content,
                                                  string responseConnectionId,
                                                  string errorMessage)
    {
        return new ExternalQueryState
        {
            Content = null,
            ResponsePayload = content,
            Action = ActionType.GetOrder,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetOrderFailed,
            UniqueConnectionId = responseConnectionId,
            Description = errorMessage,
        };
    }

    public static ExternalQueryState InvalidPosition(string errorMessage)
    {
        return new ExternalQueryState
        {
            Content = null,
            ResponsePayload = null,
            Action = ActionType.GetPosition,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetPositionFailed,
            UniqueConnectionId = "",
            Description = errorMessage,
        };
    }

    public static ExternalQueryState QueryOrder(Order order,
                                                string content,
                                                string responseConnectionId)
    {
        return new ExternalQueryState
        {
            Content = order,
            ResponsePayload = content,
            Action = ActionType.GetOrder,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetOrderOk,
            UniqueConnectionId = responseConnectionId,
            Description = $"Got one order for {order.SecurityCode}.",
        };
    }

    public static ExternalQueryState InvalidTrade(string content,
                                                  string responseConnectionId,
                                                  string errorMessage)
    {
        return new ExternalQueryState
        {
            Content = null,
            ResponsePayload = content,
            Action = ActionType.GetTrade,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetTradeFailed,
            UniqueConnectionId = responseConnectionId,
            Description = errorMessage,
        };
    }

    public static ExternalQueryState QueryTrades(string content,
                                                 string connId,
                                                 string securityCode,
                                                 List<Trade> trades)
    {
        return new ExternalQueryState
        {
            Content = trades.IsNullOrEmpty() ? trades : trades.Count == 1 ? trades[0] : trades,
            ResponsePayload = content,
            Action = ActionType.GetTrade,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetTradeOk,
            UniqueConnectionId = connId,
            Description = $"Got {trades.Count} trade(s) for {securityCode}.",
        };
    }

    public static ExternalQueryState QueryTrade(string content,
                                                string connId,
                                                string securityCode,
                                                Trade trade)
    {
        return new ExternalQueryState
        {
            Content = trade,
            ResponsePayload = content,
            Action = ActionType.GetTrade,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetTradeOk,
            UniqueConnectionId = connId,
            Description = $"Got a trade for {securityCode}.",
        };
    }

    public static ExternalQueryState CancelOrders(string securityCode,
                                                  string content,
                                                  string connId,
                                                  List<Order>? orders = null, Order? order = null)
    {
        var isOk = content != "" && content != "{}";

        return new ExternalQueryState
        {
            Content = order != null ? order : orders,
            ResponsePayload = content,
            Action = ActionType.CancelOrder,
            ExternalId = BrokerId,
            ResultCode = isOk ? ResultCode.CancelOrderOk : ResultCode.CancelOrderFailed,
            UniqueConnectionId = connId,
            Description = $"Cancelled {(order != null ? 1 : orders?.Count)} orders of security " + securityCode,
        };
    }


    public static ExternalQueryState Error(ActionType actionType, ResultCode resultCode, string? content, string connId, string errorMessage)
    {
        return new ExternalQueryState
        {
            Content = null,
            ResponsePayload = content,
            Action = actionType,
            ExternalId = BrokerId,
            ResultCode = resultCode,
            UniqueConnectionId = connId,
            Description = errorMessage,
        };
    }

    public static ExternalQueryState QueryOrders(string? code,
                                                 string content,
                                                 string connId,
                                                 List<Order>? orders = null,
                                                 Order? order = null)
    {
        return new ExternalQueryState
        {
            Content = order != null ? order : orders,
            ResponsePayload = content,
            Action = ActionType.GetOrder,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetOrderOk,
            UniqueConnectionId = connId,
            Description = $"Got {(order != null ? 1 : orders?.Count)} open order(s)" + (code.IsBlank() ? "" : " for security: " + code),
        };
    }

    public static ExternalQueryState UpdateOrder(Order cancelledOrder, Order updatedOrder, ExternalQueryState cancelState, ExternalQueryState sendState)
    {
        var compositeResult = cancelState.ResultCode switch
        {
            ResultCode.CancelOrderOk when sendState.ResultCode == ResultCode.SendOrderOk => ResultCode.UpdateOrderOk,
            ResultCode.CancelOrderOk when sendState.ResultCode == ResultCode.SendOrderFailed => ResultCode.UpdateOrderSendFailed,
            ResultCode.CancelOrderFailed => ResultCode.UpdateOrderCancelFailed,
            _ => throw new InvalidOperationException($"Invalid combination of result codes from cancel state ({cancelState.ResultCode}) and send state ({sendState.ResultCode})"),
        };
        var state = new ExternalQueryState
        {
            Content = null,
            ResponsePayload = null,
            Action = ActionType.UpdateOrder,
            ExternalId = BrokerId,
            ResultCode = compositeResult,
            UniqueConnectionId = null,
            Description = $"Update order: cancel then send a new order for security code {cancelledOrder.SecurityCode}",
            SubStates = new(),
        };
        state.SubStates.Add(cancelState);
        state.SubStates.Add(sendState);
        return state;
    }

    public static ExternalQueryState QueryAccount(string? content, string? connId, Account account)
    {
        return new ExternalQueryState
        {
            Content = account,
            ResponsePayload = content,
            Action = ActionType.GetAccount,
            ExternalId = BrokerId,
            ResultCode = account == null ? ResultCode.GetAccountFailed : ResultCode.GetAccountOk,
            UniqueConnectionId = connId,
            Description = $"Get account.",
        };
    }

    public static ExternalQueryState QueryAssets(string? content, string? connId, List<Asset> assets)
    {
        return new ExternalQueryState
        {
            Content = assets,
            ResponsePayload = content,
            Action = ActionType.GetAccount,
            ExternalId = BrokerId,
            ResultCode = assets == null ? ResultCode.GetBalanceFailed : assets.Count == 0 ? ResultCode.NoBalance : ResultCode.GetBalanceFailed,
            UniqueConnectionId = connId,
            Description = $"Get {assets?.Count} assets.",
        };
    }

    public static ExternalQueryState SendOrder(Order order, string content, string connId, bool isOk)
    {
        return new ExternalQueryState
        {
            Content = order,
            ResponsePayload = content,
            Action = ActionType.SendOrder,
            ExternalId = BrokerId,
            ResultCode = isOk ? ResultCode.SendOrderOk : ResultCode.SendOrderFailed,
            UniqueConnectionId = connId,
            Description = $"Send {order.SecurityCode} order.",
        };
    }

    public static ExternalQueryState QueryMisc(string content, string connId, object obj)
    {
        if (obj == null) throw new InvalidOperationException("Invalid usage of " + nameof(QueryMisc));
        return new ExternalQueryState
        {
            Content = obj,
            ResponsePayload = content,
            Action = ActionType.GetTrade,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetTradeOk,
            UniqueConnectionId = connId,
            Description = "",
        };
    }
}

public static class ExternalConnectionStates
{
    public static EnvironmentType Environment { get; set; }
    public static ExchangeType Exchange { get; set; }
    public static int EnvironmentId { get; set; }
    public static BrokerType Broker { get; set; }
    public static int BrokerId { get; set; }

    public static ExternalConnectionState SubscribedHistoricalOhlcOk(Security security, DateTime start, DateTime end)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.SubscriptionOk,
            ExternalId = BrokerId,
            Description = $"Subscribed OHLC price from {start:yyyyMMdd-HHmmss} to {end:yyyyMMdd-HHmmss}",
            Type = SubscriptionType.HistoricalMarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState SubscribedOhlcFailed(Security security, string errorDescription)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.InvalidArgument,
            ExternalId = BrokerId,
            Description = errorDescription,
            Type = SubscriptionType.MarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState AlreadySubscribedRealTimeOhlc(Security security, IntervalType interval)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.AlreadySubscribed,
            ExternalId = BrokerId,
            Description = $"Subscribed OHLC price for {security.Id} with interval {interval}",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState UnsubscribedRealTimeOhlcOk(Security security, IntervalType interval)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.UnsubscriptionOk,
            ExternalId = BrokerId,
            Description = $"Unsubscribed OHLC price for {security.Id} with interval {interval}",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState UnsubscribedRealTimeOhlcFailed(Security security, IntervalType interval)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.UnsubscriptionOk,
            ExternalId = BrokerId,
            Description = $"Failed to unsubscribe OHLC price for {security.Id} with interval {interval}",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState UnsubscribedMultipleRealTimeOhlc(List<ExternalConnectionState> subStates)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.MultipleUnsubscriptionOk,
            ExternalId = BrokerId,
            Description = $"Unsubscribed ({subStates.Count}) OHLC prices",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
            SubStates = subStates,
        };
    }

    public static ExternalConnectionState StillHasSubscribedRealTimeOhlc(Security security, IntervalType interval)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Unsubscribe,
            ResultCode = ResultCode.StillHasSubscription,
            ExternalId = BrokerId,
            Description = $"Will not unsubscribed OHLC price for {security.Id} with interval {interval} because of other subscribers",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }

    public static ExternalConnectionState Subscribed(SubscriptionType type, string description = "Subscribed")
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.SubscriptionOk,
            ExternalId = BrokerId,
            Description = description,
            Type = type,
        };
    }

    public static ExternalConnectionState SubscriptionFailed(SubscriptionType type, string description = "Subscribed")
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            ResultCode = ResultCode.SubscriptionFailed,
            ExternalId = BrokerId,
            Description = description,
            Type = type,
        };
    }
}