namespace TradeCommon.Essentials.Trading;
public enum OriginType
{
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered manually.
    /// </summary>
    Manual,
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered by orders sent to external when the main order is just sent.
    /// </summary>
    UpfrontOrder,
    /// <summary>
    /// All stop orders (SL/TP) are created / triggered by tick price events.
    /// </summary>
    TickSignal,
}
