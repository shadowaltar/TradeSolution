using Common;
using TradeCommon.Constants;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Action to buy or sell a security.
/// </summary>
public class Order
{
    public const long DefaultId = 0;

    /// <summary>
    /// Unique order id.
    /// </summary>
    [UpsertConflictKey]
    public long Id { get; set; }

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// If this is a new order it should be null.
    /// </summary>
    public long ExternalOrderId { get; set; } = DefaultId;

    /// <summary>
    /// The id of security to be or already being bought / sold.
    /// </summary>
    public int SecurityId { get; set; }

    /// <summary>
    /// The code / symbol / ticker for execution.
    /// It caches the value from <see cref="Security"/> object and should not
    /// be persisted.
    /// </summary>
    [UpsertIgnore]
    public string SecurityCode { get; set; } = "";

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
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// Order update / cancel time (client-side).
    /// </summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// Order creation time (external system).
    /// This is optional (DateTime.MinValue).
    /// </summary>
    public DateTime ExternalCreateTime { get; set; }

    /// <summary>
    /// Order update / cancel time (external system).
    /// This is optional (DateTime.MinValue).
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
    /// The broker's ID.
    /// </summary>
    public int BrokerId { get; set; } = BrokerIds.NameToIds[ExternalNames.Unknown];

    /// <summary>
    /// The exchange's ID.
    /// </summary>
    public int ExchangeId { get; set; } = ExchangeIds.GetId(ExternalNames.Unknown);

    /// <summary>
    /// Any additional order parameters.
    /// </summary>
    [UpsertIgnore]
    public AdvancedOrderSettings? AdvancedOrderSettings { get; set; }

    public override string ToString()
    {
        return $"[{Id}][{ExternalOrderId}][{CreateTime:yyMMdd-HHmmss}][{Status}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}";
    }
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
