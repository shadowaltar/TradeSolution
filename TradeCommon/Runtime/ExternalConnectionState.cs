namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ConnectionActionType Action { get; set; }
    public string? StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }

    public override string ToString()
    {
        return $"ConnState [{Type}] action [{Action}] [{StatusCode}][{UniqueConnectionId}]";
    }
}

public class ExternalExecutionState
{
    public ExecutionActionType Action { get; set; }
    public int StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }

    public override string ToString()
    {
        return $"ExecState action [{Action}] [{StatusCode}][{UniqueConnectionId}]";
    }
}

public enum SubscriptionType
{
    Unknown,
    QuotationService,
    RealTimeMarketData,
}

public enum ConnectionActionType
{
    Unknown,
    Connect,
    Disconnect,
    Subscribe,
    Unsubscribe,
}

public enum ExecutionActionType
{
    Unknown,
    SendOrder,
    CancelOrder,
}