namespace TradeCommon.Runtime;

public enum ResultCode
{
    Unknown,
    NoAction,

    Ok,
    Conflict,
    Failed,

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
    GetPriceOk,
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

    AlreadyLoggedIn,
    NotLoggedInYet,
    ActiveAlgoBatchesExist,

    GetMiscFailed,
    GetUserFailed,
    GetAccountFailed,
    GetBalanceFailed,
    GetSomeAccountsFailed,
    GetPriceFailed,
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


    ClockOutOfSync,
    MessageResponseOutOfTimeWindow,
    InvalidAssetQuantity,
}
