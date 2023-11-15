namespace TradeCommon.Essentials.Trading;
public enum StopOrderStyleType
{
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered manually.
    /// </summary>
    Manual,
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered by read SL/TP orders which are sent to external.
    /// </summary>
    RealOrder,
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered by tick price events.
    /// </summary>
    TickSignal,
}
