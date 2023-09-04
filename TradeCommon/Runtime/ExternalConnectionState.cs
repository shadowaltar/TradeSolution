﻿using Azure;
using BenchmarkDotNet.Running;
using Common;
using Iced.Intel;
using OfficeOpenXml.Style;
using System.Diagnostics;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ActionType Action { get; set; }
    public string? StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }
    public List<ExternalConnectionState>? SubStates { get; set; }
    public override string ToString()
    {
        return $"ConnState [{Type}] action [{Action}] [{StatusCode}][{UniqueConnectionId}]";
    }
}

public class ExternalQueryState : INetworkTimeState
{
    public object? Content { get; set; }

    public string? ResponsePayload { get; set; }

    public ActionType Action { get; set; }

    public ResultCode ResultCode { get; set; }

    public int ExternalId { get; set; }

    public string? UniqueConnectionId { get; set; }

    public string? Description { get; set; }

    public long NetworkRoundtripTime { get; set; }

    public long TotalTime { get; set; }

    public T? ContentAs<T>() => Content is T typed ? typed : default;

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

    public override string ToString()
    {
        return $"State Time[{NetworkRoundtripTime}ms/{TotalTime}ms] Action[{Action}] [{ResultCode}] [{UniqueConnectionId}]";
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

    GetFrequencyRestriction,
}


public static class ExternalQueryStates
{
    public static EnvironmentType Environment { get; set; }
    public static ExchangeType Exchange { get; set; }
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
                                                 params Trade[] trades)
    {
        if (trades.Length == 0) throw new InvalidOperationException("Invalid usage of " + nameof(QueryTrades));
        return new ExternalQueryState
        {
            Content = trades.Length == 1 ? trades[0] : trades,
            ResponsePayload = content,
            Action = ActionType.GetTrade,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetTradeOk,
            UniqueConnectionId = connId,
            Description = $"Got {trades.Length} trade(s) for {trades[0].SecurityCode}.",
        };
    }

    public static ExternalQueryState CancelOrders(string securityCode,
                                                  string content,
                                                  string connId,
                                                  params Order[] orders)
    {
        var isOk = content != "" && content != "{}";

        return new ExternalQueryState
        {
            Content = orders,
            ResponsePayload = content,
            Action = ActionType.CancelOrder,
            ExternalId = BrokerId,
            ResultCode = isOk ? ResultCode.CancelOrderOk : ResultCode.CancelOrderFailed,
            UniqueConnectionId = connId,
            Description = "Cancelled all orders of security " + securityCode,
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

    public static ExternalQueryState QueryOrders(string? code, string content, string connId, Order[] orders)
    {
        return new ExternalQueryState
        {
            Content = orders,
            ResponsePayload = content,
            Action = ActionType.GetOrder,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetOrderOk,
            UniqueConnectionId = connId,
            Description = $"Got {orders.Length} open order(s)" + (code.IsBlank() ? "" : " for security: " + code),
        };
    }

    public static ExternalQueryState UpdateOrder(Order cancelledOrder, Order updatedOrder, ExternalQueryState cancelState, ExternalQueryState sendState)
    {
        ResultCode compositeResult;
        switch (cancelState.ResultCode)
        {
            case ResultCode.CancelOrderOk when sendState.ResultCode == ResultCode.SendOrderOk:
                compositeResult = ResultCode.UpdateOrderOk;
                break;
            case ResultCode.CancelOrderOk when sendState.ResultCode == ResultCode.SendOrderFailed:
                compositeResult = ResultCode.UpdateOrderSendFailed;
                break;
            case ResultCode.CancelOrderFailed:
                compositeResult = ResultCode.UpdateOrderCancelFailed;
                break;
            default:
                throw new InvalidOperationException($"Invalid combination of result codes from cancel state ({cancelState.ResultCode}) and send state ({sendState.ResultCode})");
        }
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

    public static ExternalQueryState QueryAccounts(string? content, string? connId, params Account[] accounts)
    {
        return new ExternalQueryState
        {
            Content = accounts.Length == 1 ? accounts[0] : accounts,
            ResponsePayload = content,
            Action = ActionType.GetAccount,
            ExternalId = BrokerId,
            ResultCode = accounts.All(a => a != null) ? ResultCode.GetAccountOk : accounts.All(a => a == null) ? ResultCode.GetAccountFailed : ResultCode.GetSomeAccountsFailed,
            UniqueConnectionId = connId,
            Description = accounts.Length == 1 ? $"Get an account" : $"Get {accounts.Length} account(s)",
        };
    }

    public static ExternalQueryState InvalidAccount(string content, string connId)
    {
        return new ExternalQueryState
        {
            Content = null,
            ResponsePayload = content,
            Action = ActionType.GetAccount,
            ExternalId = BrokerId,
            ResultCode = ResultCode.GetAccountFailed,
            UniqueConnectionId = connId,
            Description = "Failed to get account info",
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
            Description = content,
        };
    }
}

public static class ExternalConnectionStates
{
    public static ExternalConnectionState SubscribedHistoricalOhlcOk(Security security, DateTime start, DateTime end)
    {
        return new ExternalConnectionState
        {
            Action = ActionType.Subscribe,
            StatusCode = nameof(ResultCode.SubscriptionOk),
            ExternalPartyId = security.Exchange,
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
            StatusCode = nameof(ResultCode.InvalidArgument),
            ExternalPartyId = security.Exchange,
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
            StatusCode = nameof(ResultCode.AlreadySubscribed),
            ExternalPartyId = security.Exchange,
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
            StatusCode = nameof(ResultCode.UnsubscriptionOk),
            ExternalPartyId = security.Exchange,
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
            StatusCode = nameof(ResultCode.UnsubscriptionOk),
            ExternalPartyId = security.Exchange,
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
            StatusCode = nameof(ResultCode.MultipleUnsubscriptionOk),
            ExternalPartyId = null,
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
            StatusCode = nameof(ResultCode.StillHasSubscription),
            ExternalPartyId = security.Exchange,
            Description = $"Will not unsubscribed OHLC price for {security.Id} with interval {interval} because of other subscribers",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }
}