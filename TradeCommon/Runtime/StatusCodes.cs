namespace TradeCommon.Runtime;

public static class StatusCodes
{
    public const int ConnectionOk = 100;
    public const int DisconnectionOk = 101;
    public const int SubscriptionOk = 102;
    public const int UnsubscriptionOk = 103;
    
    public const int SubscriptionWaiting = 202;
    public const int UnsubscriptionWaiting = 202;

    public const int InvalidArgument = 400;
    public const int InvalidCredential = 401;

    public const int NoAliveExternals = 450;

    public const int ConnectionFailed = 500;
    public const int DisconnectionFailed = 501;
    public const int SubscriptionFailed = 502;
    public const int UnsubscriptionFailed = 503;
}
