using Common;
using TradeCommon.Constants;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Action to buy or sell a security.
/// </summary>
public class Order : IComparable<Order>
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
    /// Security code (will not be saved to database).
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
    public decimal Price { get; set; } = 0;

    /// <summary>
    /// Price of a stop order.
    /// </summary>
    public decimal StopPrice { get; set; } = 0;

    /// <summary>
    /// Quantity to be traded.
    /// </summary>
    public decimal Quantity { get; set; } = 0;

    /// <summary>
    /// Currently filled quantity.
    /// </summary>
    public decimal FilledQuantity { get; set; } = 0;

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
    public int BrokerId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The exchange's ID.
    /// </summary>
    public int ExchangeId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// Any additional order parameters.
    /// </summary>
    [UpsertIgnore]
    public AdvancedOrderSettings? AdvancedOrderSettings { get; set; }

    /// <summary>
    /// Gets if the order is successfully placed (either it is still alive or filled).
    /// </summary>
    [UpsertIgnore]
    public bool IsSuccessful => Status is OrderStatus.Live or OrderStatus.Filled or OrderStatus.PartialFilled;

    public int CompareTo(Order? other)
    {
        var r = ExternalOrderId.CompareTo(other?.ExternalOrderId);
        if (r != 0) r = SecurityId.CompareTo(other?.SecurityId);
        if (r != 0) r = AccountId.CompareTo(other?.AccountId);
        if (r != 0) r = Type.CompareTo(other?.Type);
        if (r != 0) r = Side.CompareTo(other?.Side);
        if (r != 0) r = Price.CompareTo(other?.Price);
        if (r != 0) r = StopPrice.CompareTo(other?.StopPrice);
        if (r != 0) r = Quantity.CompareTo(other?.Quantity);
        if (r != 0) r = FilledQuantity.CompareTo(other?.FilledQuantity);
        if (r != 0) r = Status.CompareTo(other?.Status);
        if (r != 0) r = CreateTime.CompareTo(other?.CreateTime);
        if (r != 0) r = UpdateTime.CompareTo(other?.UpdateTime);
        if (r != 0) r = ExternalCreateTime.CompareTo(other?.ExternalCreateTime);
        if (r != 0) r = ExternalUpdateTime.CompareTo(other?.ExternalUpdateTime);
        if (r != 0) r = TimeInForce.CompareTo(other?.TimeInForce);
        if (r != 0) r = BrokerId.CompareTo(other?.BrokerId);
        if (r != 0) r = ExchangeId.CompareTo(other?.ExchangeId);
        if (AdvancedOrderSettings != null)
        {
            if (r != 0) r = AdvancedOrderSettings.TimeInForceTime.CompareTo(other?.AdvancedOrderSettings?.TimeInForceTime);
            if (r != 0) r = AdvancedOrderSettings.TrailingSpread.CompareTo(other?.AdvancedOrderSettings?.TrailingSpread);
            if (r != 0) r = AdvancedOrderSettings.TrailingType.CompareTo(other?.AdvancedOrderSettings?.TrailingType);
            if (r != 0) r = AdvancedOrderSettings.TrailingValue.CompareTo(other?.AdvancedOrderSettings?.TrailingValue);
        }
        return r;
    }

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
