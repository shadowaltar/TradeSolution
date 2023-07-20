namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Status enum for an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Unknown status (usually invalid).
    /// </summary>
    Unknown,
    /// <summary>
    /// The order is to be placed and not acknowledged by broker yet.
    /// </summary>
    Placing,
    /// <summary>
    /// The order is to be modified and not acknowledged by broker yet.
    /// </summary>
    Modifying,
    /// <summary>
    /// The order is to be canceled and not acknowledged by broker yet.
    /// </summary>
    Canceling,

    /// <summary>
    /// Broker is pending for submitting to exchange.
    /// </summary>
    WaitingSubmit,
    /// <summary>
    /// Broker submitted the order to exchange and waiting for feedback.
    /// </summary>
    Submitting,
    /// <summary>
    /// The order is submitted and alive in exchange.
    /// </summary>
    Live,
    /// <summary>
    /// The order is partially filled and still alive in exchange.
    /// </summary>
    PartialFilled,
    /// <summary>
    /// The order is partially filled and already canceled in exchange.
    /// </summary>
    PartialCanceled,
    /// <summary>
    /// The order is not filled at all and already canceled in exchange.
    /// </summary>
    Canceled,
    /// <summary>
    /// The order is failed, service denied.
    /// </summary>
    Failed,
    /// <summary>
    /// Synonym of <see cref="Failed"/>.
    /// </summary>
    Rejected,
    /// <summary>
    /// The order is expired due to <see cref="OrderTimeInForceType"/> and / or <see cref="OrderTimeInForceType"/> setting.
    /// </summary>
    Expired,
    /// <summary>
    /// The order is deleted: it never reached exchange and canceled on broker side only.
    /// </summary>
    Deleted,
}