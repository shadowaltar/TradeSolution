namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public SubscriptionType Type { get; set; }
    public ConnectionActionType Action { get; set; }
    public string? StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }
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