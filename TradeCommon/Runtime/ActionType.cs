namespace TradeCommon.Runtime;

public enum ActionType
{
    Unknown,
    Any,

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
    GetAssetPosition,
    GetPrice,

    GetMisc,
    GetSubscription,
    GetFrequencyRestriction,

    Deposit,
    Withdraw,
    ManualStopLoss,
}
