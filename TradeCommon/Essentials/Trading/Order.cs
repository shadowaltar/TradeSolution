namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Action to buy or sell a security.
/// </summary>
public class Order
{
    /// <summary>
    /// Unique order id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// If this is a new order it should be null.
    /// </summary>
    public string? ExternalOrderId { get; set; }

    /// <summary>
    /// The id of security to be or already being bought / sold.
    /// </summary>
    public int SecurityId { get; set; }

    /// <summary>
    /// The target account. This embeds the broker info.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// The type of order.
    /// </summary>
    public OrderType Type { get; set; } = OrderType.Market;

    /// <summary>
    /// To buy / sell.
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Price of a (limit) order. For a <see cref="OrderType.Market"/> order
    /// this field is meaningless.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Price of a stop order.
    /// </summary>
    public decimal StopPrice { get; set; } = decimal.MinValue;

    /// <summary>
    /// Quantity to be traded.
    /// </summary>
    public decimal Quantity { get; set; } = decimal.MinValue;

    /// <summary>
    /// Status of this order
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Unknown;

    /// <summary>
    /// Order creation time (client-side).
    /// </summary>
    public DateTime ClientCreateTime { get; set; }

    /// <summary>
    /// Order update / cancel time (client-side).
    /// </summary>
    public DateTime ClientUpdateTime { get; set; }

    /// <summary>
    /// Order creation time (external system).
    /// </summary>
    public DateTime ExternalCreateTime { get; set; }

    /// <summary>
    /// Order update / cancel time (external system).
    /// </summary>
    public DateTime ExternalUpdateTime { get; set; }

    /// <summary>
    /// Indicates how the order will be cancelled or stay alive, like GTC / FOK etc.
    /// </summary>
    public OrderTimeInForceType TimeInForce { get; set; } = OrderTimeInForceType.GoodTillCancel;

    /// <summary>
    /// The strategy used to generate this order.
    /// </summary>
    public int StrategyId { get; set; }

    /// <summary>
    /// Any additional order parameters.
    /// </summary>
    public AdvancedOrderSettings AdvancedOrderSettings { get; set; }
}

public class AdvancedOrderSettings
{
    public OrderTrailingType TrailingType { get; set; }
    public double TrailingValue { get; set; }
    public double TrailingSpread { get; set; }
    public DateTime TimeInForceTime { get; set; }
}

public enum OrderTrailingType
{
    None,
    Ratio,
    Amount,
}
