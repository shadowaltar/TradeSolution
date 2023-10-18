namespace TradeCommon.Runtime;

public enum ResultCode
{
    Unknown,
    NoAction,

    ConnectionOk,
    DisconnectionOk,
    SubscriptionOk,
    UnsubscriptionOk,
    MultipleUnsubscriptionOk,

    StartEngineOk,
    StopEngineOk,

    GetMiscOk,
    GetUserOk,
    GetAccountOk,
    GetBalanceOk,
    LoginUserOk,
    LoginAccountOk,
    LoginUserAndAccountOk,

    GetOrderOk,
    SendOrderOk,
    CancelOrderOk,
    UpdateOrderOk,

    GetTradeOk,
    
    GetPositionOk,

    GetSecretOk,

    AlreadySubscribed,
    StillHasSubscription,

    SubscriptionWaiting = 1000,
    UnsubscriptionWaiting,
    
    NoBalance,

    InvalidArgument = 4000,
    InvalidCredential,

    NoAliveExternals,

    ConnectionFailed,
    DisconnectionFailed,
    SubscriptionFailed,
    UnsubscriptionFailed,
    
    StartEngineFailed,
    StopEngineFailed,

    GetMiscFailed,
    GetUserFailed,
    GetAccountFailed,
    GetBalanceFailed,
    GetSomeAccountsFailed,
    AccountNotOwnedByUser,    
    LoginUserFailed,
    LoginAccountFailed,
    LoginUserAndAccountFailed,

    GetOrderFailed,
    SendOrderFailed,
    CancelOrderFailed,
    UpdateOrderCancelFailed,
    UpdateOrderSendFailed,

    GetTradeFailed,
    
    GetPositionFailed,

    GetSecretFailed,
    SecretMalformed,
}
