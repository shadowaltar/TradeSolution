namespace TradeCommon.Runtime;

public enum ResultCode
{
    Unknown,

    ConnectionOk,
    DisconnectionOk,
    SubscriptionOk,
    UnsubscriptionOk,
    MultipleUnsubscriptionOk,
    
    GetMiscOk,
    GetUserOk,
    GetAccountOk,
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

    InvalidArgument = 4000,
    InvalidCredential,

    NoAliveExternals,

    ConnectionFailed,
    DisconnectionFailed,
    SubscriptionFailed,
    UnsubscriptionFailed,

    GetMiscFailed,
    GetUserFailed,
    GetAccountFailed,
    GetSomeAccountsFailed,
    AccountNotOwnedByUser,
    AccountHasNoAsset,
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
