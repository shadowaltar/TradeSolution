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
    ///// <summary>
    ///// The order is submitted in exchange. Alias of <see cref="Live"/>.
    ///// Used by:
    /////     Binance
    ///// </summary>
    //New = 10,
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

public static class OrderStatuses
{
    public static bool IsFinished(this OrderStatus status)
    {
        return status == OrderStatus.Filled || status.IsFinishedWithoutFilled();
    }

    public static bool IsFinishedWithoutFilled(this OrderStatus status)
    {
        return status is OrderStatus.Cancelled
            or OrderStatus.Expired
            or OrderStatus.Deleted
            or OrderStatus.Failed
            or OrderStatus.Prevented
            or OrderStatus.Rejected;
    }

    public static OrderStatus[] Lives { get; } = new OrderStatus[]
    {
        OrderStatus.Sending, OrderStatus.Live, OrderStatus.PartialFilled
    };

    public static OrderStatus[] Fills { get; } = new OrderStatus[]
    {
        OrderStatus.Filled, OrderStatus.PartialFilled, OrderStatus.PartialCancelled
    };

    public static OrderStatus[] Errors { get; } = new OrderStatus[]
    {
        OrderStatus.Failed, OrderStatus.Prevented, OrderStatus.Rejected
    };

    public static OrderStatus[] Cancels { get; } = new OrderStatus[]
    {
        OrderStatus.Canceling, OrderStatus.Cancelled, OrderStatus.PartialCancelled, OrderStatus.Deleted, OrderStatus.Expired
    };

    public static bool IsAlive(this OrderStatus status)
    {
        return Array.IndexOf(Lives, status) != -1;
    }

    public static bool IsFilled(this OrderStatus status)
    {
        return Array.IndexOf(Fills, status) != -1;
    }

    public static bool IsErroneous(this OrderStatus status)
    {
        return Array.IndexOf(Errors, status) != -1;
    }

    public static bool IsCancel(this OrderStatus status)
    {
        return Array.IndexOf(Cancels, status) != -1;
    }

    public static OrderStatus ParseBinance(string? statusStr)
    {
        if (statusStr == null)
            return OrderStatus.Unknown;

        statusStr = statusStr.Trim().ToUpperInvariant();
        return statusStr switch
        {
            "NEW" => OrderStatus.Live,
            "PARTIAL_FILLED" or "PARTIALLY_FILLED" or "TRADE" => OrderStatus.PartialFilled,
            "FILLED" => OrderStatus.Filled,
            "CANCELED" => OrderStatus.Cancelled,
            "REJECTED" => OrderStatus.Rejected,
            "EXPIRED" or "EXPIRED_IN_MATCH" => OrderStatus.Expired,
            "TRADE_PREVENTION" => OrderStatus.Prevented,
            _ => OrderStatus.Unknown
        };
    }
}