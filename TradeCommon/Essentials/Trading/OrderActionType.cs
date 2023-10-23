namespace TradeCommon.Essentials.Trading;

public enum OrderActionType
{
    Unknown,

    /// <summary>
    /// Operational order which its trades should not be considered opening/closing any positions.
    /// </summary>
    Operational,

    ManualOpen,
    ManualClose,
    ManualAdjust,
    ManualCancel,
    /// <summary>
    /// A SL order placed manually.
    /// </summary>
    ManualPlacedStopLoss,
    /// <summary>
    /// A TP order placed manually.
    /// </summary>
    ManualPlacedTakeProfit,

    AlgoOpen,
    AlgoAdjust,
    AlgoClose,
    AlgoCancel,
    /// <summary>
    /// A SL order placed by algorithm.
    /// </summary>
    AlgoPlacedStopLoss,
    /// <summary>
    /// A TP order placed by algorithm.
    /// </summary>
    AlgoPlacedTakeProfit,
    /// <summary>
    /// A normal order to close position which mimics an external SL order.
    /// </summary>
    AlgoCloseAsStopLoss,
    /// <summary>
    /// A normal order to close position which mimics an external TP order.
    /// </summary>
    AlgoCloseAsTakeProfit,

    /// <summary>
    /// A normal order to close position in order to clean up anything opened.
    /// </summary>
    CleanUpLive,

    /// <summary>
    /// An order to cancel any order which is still opened.
    /// </summary>
    UrgentKill,
}
