namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ExternalActionType Action { get; set; }
    public string? StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }

    public override string ToString()
    {
        return $"ConnState [{Type}] action [{Action}] [{StatusCode}][{UniqueConnectionId}]";
    }
}

public class ExternalQueryState<T> : INetworkTimeState
{
    public T? Content { get; set; }

    public string? ResponsePayload { get; set; }

    public ExternalActionType Action { get; set; }
    public int StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }

    public long NetworkRoundtripTime { get; set; }
    public long TotalTime { get; set; }

    public override string ToString()
    {
        return $"ExecState action [{Action}] [{StatusCode}][{UniqueConnectionId}]";
    }
}

public interface INetworkTimeState
{
    long NetworkRoundtripTime { get; set; }
    long TotalTime { get; set; }
}

public class ExternalQueryState : ExternalQueryState<object> { }

public enum SubscriptionType
{
    Unknown,
    QuotationService,
    RealTimeMarketData,
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