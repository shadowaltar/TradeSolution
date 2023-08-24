﻿namespace TradeCommon.Runtime;

public static class StatusCodes
{
    public const int ConnectionOk = 1000;
    public const int DisconnectionOk = 1001;
    public const int SubscriptionOk = 1002;
    public const int UnsubscriptionOk = 1003;
    public const int MultipleUnsubscription = 1004;
    public const int SendOrderOk = 1010;
    public const int CancelOrderOk = 1011;
    public const int GetUserOk = 1020;
    public const int GetAccountOk = 1021;

    public const int AlreadySubscribed = 1100;
    public const int StillHasSubscription = 1101;

    public const int SubscriptionWaiting = 2002;
    public const int UnsubscriptionWaiting = 2003;

    public const int InvalidArgument = 4000;
    public const int InvalidCredential = 4001;

    public const int NoAliveExternals = 4050;

    public const int ConnectionFailed = 5000;
    public const int DisconnectionFailed = 5001;
    public const int SubscriptionFailed = 5002;
    public const int UnsubscriptionFailed = 5003;
    public const int SendOrderFailed = 5010;
    public const int CancelOrderFailed = 5011;
    public const int GetUserFailed = 5020;
    public const int GetAccountFailed = 5021;
}
