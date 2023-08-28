using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ExternalActionType Action { get; set; }
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

    public ExternalActionType Action { get; set; }
    public int StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }

    public long NetworkRoundtripTime { get; set; }
    public long TotalTime { get; set; }

    public T? ContentAs<T>() => Content is T typed ? typed : default;

    public override string ToString()
    {
        return $"ExecState Time[{NetworkRoundtripTime}ms/{TotalTime}ms]Action[{Action}] [{StatusCode}] [{UniqueConnectionId}]";
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

public enum ExternalActionType
{
    Unknown,
    Connect,
    Disconnect,
    Subscribe,
    Unsubscribe,

    SendOrder,
    CancelOrder,
    CancelAllOrders,

    GetAccount,
    GetTrades,
    GetOrders,

    CheckOrderSpeedLimit,
}


public static class ExternalQueryStates
{
    public static ExternalQueryState InvalidSecurity(ExternalActionType action)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalPartyId = null,
            StatusCode = StatusCodes.InvalidArgument,
            Description = $"Invalid or missing security.",
        };
    }

    public static ExternalQueryState InvalidArgument(ExternalActionType action, string message)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalPartyId = null,
            StatusCode = StatusCodes.InvalidArgument,
            Description = message,
        };
    }

    public static ExternalQueryState InvalidExchange(ExternalActionType action, string externalName)
    {
        return new ExternalQueryState
        {
            Content = default,
            ResponsePayload = null,
            Action = action,
            ExternalPartyId = externalName,
            StatusCode = StatusCodes.InvalidArgument,
            Description = $"Wrong exchange for the security; expecting {externalName}.",
        };
    }
}

public static class ExternalConnectionStates
{
    public static ExternalConnectionState SubscribedHistoricalOhlcOk(Security security, DateTime start, DateTime end)
    {
        return new ExternalConnectionState
        {
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.SubscriptionOk),
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
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.InvalidArgument),
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
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.AlreadySubscribed),
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
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.UnsubscriptionOk),
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
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.UnsubscriptionOk),
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
            Action = ExternalActionType.Subscribe,
            StatusCode = nameof(StatusCodes.MultipleUnsubscription),
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
            Action = ExternalActionType.Unsubscribe,
            StatusCode = nameof(StatusCodes.StillHasSubscription),
            ExternalPartyId = security.Exchange,
            Description = $"Will not unsubscribed OHLC price for {security.Id} with interval {interval} because of other subscribers",
            Type = SubscriptionType.RealTimeMarketData,
            UniqueConnectionId = "",
        };
    }
}