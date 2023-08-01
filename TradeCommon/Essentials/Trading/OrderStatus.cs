using BenchmarkDotNet.Attributes;

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
    /// The order is to be cancelled and not acknowledged by broker yet.
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
    /// The order is partially filled and already cancelled in exchange.
    /// </summary>
    PartialCancelled,
    /// <summary>
    /// The order is filled.
    /// </summary>
    Filled,
    /// <summary>
    /// The order is not filled at all and already cancelled in exchange.
    /// </summary>
    Cancelled,
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
    /// The order is deleted: it never reached exchange and cancelled on broker side only.
    /// </summary>
    Deleted,
}

public static class OrderStatusConverter
{
    public static OrderStatus ParseBinance(string? statusStr)
    {
        if (statusStr == null)
            return OrderStatus.Unknown;

        statusStr = statusStr.Trim().ToUpperInvariant();
        return statusStr switch
        {
            "NEW" => OrderStatus.Live,
            "PARTIALLY_FILLED" => OrderStatus.PartialFilled,
            "FILLED" => OrderStatus.Filled,
            "CANCELED" => OrderStatus.Cancelled,
            "REJECTED" => OrderStatus.Rejected,
            "EXPIRED" or "EXPIRED_IN_MATCH" => OrderStatus.Expired,
            _ => OrderStatus.Unknown
        };
    }

    public static string ToBinance(Side side)
    {
        return side switch
        {
            Side.Buy => "BUY",
            Side.Sell => "SELL",
            _ => ""
        };
    }
}