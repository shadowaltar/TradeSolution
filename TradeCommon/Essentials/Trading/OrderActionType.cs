namespace TradeCommon.Essentials.Trading;

public enum OrderActionType
{
    Unknown,

    /// <summary>
    /// Operational order which its trades should not be considered opening/closing any positions.
    /// </summary>
    Operational,

    /// <summary>
    /// A normal order to open a position, placed manually.
    /// </summary>
    ManualOpen,
    /// <summary>
    /// A normal close position order, placed manually.
    /// </summary>
    ManualClose,
    /// <summary>
    /// A normal order to adjust an open position exposure, placed manually.
    /// </summary>
    ManualAdjust,
    /// <summary>
    /// A SL order placed manually.
    /// </summary>
    ManualStopLoss,
    /// <summary>
    /// A TP order placed manually.
    /// </summary>
    ManualTakeProfit,

    /// <summary>
    /// A normal order to open a position, placed by algorithm.
    /// </summary>
    AlgoOpen,
    /// <summary>
    /// A normal close position order placed by algorithm.
    /// </summary>
    AlgoClose,
    /// <summary>
    /// A normal order to adjust an open position exposure, placed by algorithm.
    /// </summary>
    AlgoAdjust,
    /// <summary>
    /// A SL order placed by algorithm.
    /// </summary>
    AlgoStopLoss,
    /// <summary>
    /// A TP order placed by algorithm.
    /// </summary>
    AlgoTakeProfit,
    /// <summary>
    /// A normal order to close position which is created by a tick signal.
    /// </summary>
    TickSignalStopLoss,
    /// <summary>
    /// A normal order to close position which is created by a tick signal.
    /// </summary>
    TickSignalTakeProfit,

    /// <summary>
    /// A normal order to close position in order to clean up anything opened.
    /// </summary>
    CleanUpLive,

    /// <summary>
    /// An order to cancel any order which is still opened.
    /// </summary>
    UrgentKill,
}
