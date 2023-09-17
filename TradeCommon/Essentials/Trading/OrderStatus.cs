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
    Unknown = 0,
    /// <summary>
    /// Unknown status due to unrecognizable message received.
    /// </summary>
    UnknownResponse = 0,

    /// <summary>
    /// The order is to be placed and not acknowledged by broker yet.
    /// </summary>
    Sending = 1,
    /// <summary>
    /// The order is to be modified and not acknowledged by broker yet.
    /// </summary>
    Modifying = 2,
    /// <summary>
    /// The order is to be cancelled and not acknowledged by broker yet.
    /// </summary>
    Canceling = 3,

    /// <summary>
    /// Broker is pending for submitting to exchange.
    /// </summary>
    WaitingSubmit = 4,
    /// <summary>
    /// Broker submitted the order to exchange and waiting for feedback.
    /// </summary>
    Submitting = 5,
    /// <summary>
    /// The order is submitted in exchange. Alias of <see cref="Live"/>.
    /// Used by:
    ///     Binance
    /// </summary>
    New = 10,
    /// <summary>
    /// The order is submitted and alive in exchange.
    /// </summary>
    Live = 10,
    /// <summary>
    /// The order is partially filled and still alive in exchange.
    /// Used by:
    ///     Binance
    /// </summary>
    PartialFilled = 20,
    /// <summary>
    /// The order is filled.
    /// Used by:
    ///     Binance
    /// </summary>
    Filled = 30,
    /// <summary>
    /// The order is not filled at all and already cancelled in exchange.
    /// Used by:
    ///     Binance
    /// </summary>
    Cancelled = 40,
    /// <summary>
    /// The order is partially filled and already cancelled in exchange.
    /// </summary>
    PartialCancelled = 41,
    /// <summary>
    /// The order is failed, service denied.
    /// </summary>
    Failed = 50,
    /// <summary>
    /// Synonym of <see cref="Failed"/>.
    /// Used by:
    ///     Binance
    /// </summary>
    Rejected = 50,
    /// <summary>
    /// The order is expired due to <see cref="TimeInForceType"/> and / or its settings.
    /// Used by:
    ///     Binance
    /// </summary>
    Expired = 51,
    /// <summary>
    /// The order is deleted: it never reached exchange and cancelled on broker side only.
    /// </summary>
    Deleted = 52,
    /// <summary>
    /// The order is removed due to broker / exchange specific prevention rules.
    /// </summary>
    Prevented = 53,
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
            "PARTIALLY_FILLED" or "TRADE" => OrderStatus.PartialFilled,
            "FILLED" => OrderStatus.Filled,
            "CANCELED" => OrderStatus.Cancelled,
            "REJECTED" => OrderStatus.Rejected,
            "EXPIRED" or "EXPIRED_IN_MATCH" => OrderStatus.Expired,
            "TRADE_PREVENTION" => OrderStatus.Prevented,
            _ => OrderStatus.Unknown
        };
    }
}