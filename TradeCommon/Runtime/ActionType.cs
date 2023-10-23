namespace TradeCommon.Runtime;

public enum ActionType
{
    Unknown,
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
    GetPosition,
    GetPrice,

    GetMisc,
    GetSubscription,
    GetFrequencyRestriction,

    Deposit,
    Withdraw,
    ManualStopLoss,
}
