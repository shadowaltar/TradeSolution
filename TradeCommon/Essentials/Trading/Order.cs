using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Constants;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// Action to buy or sell a security.
/// </summary>
[Storage("orders", "execution")]
[Unique(nameof(Id))]
[Index(nameof(SecurityId))]
[Index(nameof(SecurityId), nameof(CreateTime))]
[Index(nameof(ExternalOrderId))]
public class Order : IComparable<Order>, ICloneable
{
    /// <summary>
    /// Unique order id.
    /// </summary>
    [NotNull, Positive]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// If this is a new order it should be null.
    /// </summary>
    [NotNull, Positive]
    public long ExternalOrderId { get; set; } = 0;

    /// <summary>
    /// The id of security to be or already being bought / sold.
    /// </summary>
    [NotNull, Positive]
    public int SecurityId { get; set; } = 0;

    /// <summary>
    /// Security code (will not be saved to database).
    /// </summary>
    [DatabaseIgnore]
    public string SecurityCode { get; set; } = "";

    /// <summary>
    /// The target account. This embeds the broker info.
    /// </summary>
    [NotNull, Positive]
    public int AccountId { get; set; } = 0;

    /// <summary>
    /// The type of order.
    /// </summary>
    [NotNull]
    public OrderType Type { get; set; } = OrderType.Market;

    /// <summary>
    /// To buy / sell.
    /// </summary>
    [NotNull]
    public Side Side { get; set; }

    /// <summary>
    /// Price of a limit order. For a <see cref="OrderType.Market"/> order
    /// this field is meaningless.
    /// </summary>
    public decimal Price { get; set; } = 0;

    /// <summary>
    /// The trigger price which when reached, a limit order (limit price specified by <see cref="Price"/>) is created.
    /// Only used in <see cref="OrderType.StopLimit"/> and <see cref="OrderType.TakeProfitLimit"/>.
    /// </summary>
    public decimal StopPrice { get; set; } = 0;

    /// <summary>
    /// Quantity to be traded.
    /// </summary>
    [NotNull]
    public decimal Quantity { get; set; } = 0;

    /// <summary>
    /// Currently filled quantity.
    /// </summary>
    [NotNull]
    public decimal FilledQuantity { get; set; } = 0;

    /// <summary>
    /// Status of this order
    /// </summary>
    [NotNull]
    public OrderStatus Status { get; set; } = OrderStatus.Unknown;

    /// <summary>
    /// Parent order Id.
    /// If a system does not support stop loss / take profit order in one go,
    /// The individual stop loss / take profit order should indicate its original order here.
    /// </summary>
    public long ParentOrderId { get; set; } = 0;

    /// <summary>
    /// Order creation time (client-side).
    /// </summary>
    [NotNull]
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// Order update / cancel time (client-side).
    /// </summary>
    [NotNull]
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
    [NotNull]
    public TimeInForceType TimeInForce { get; set; } = TimeInForceType.GoodTillCancel;

    /// <summary>
    /// The strategy used to generate this order.
    /// </summary>
    public int StrategyId { get; set; } = 0;

    /// <summary>
    /// The broker's ID.
    /// </summary>
    [NotNull]
    public int BrokerId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// The exchange's ID.
    /// </summary>
    [NotNull]
    public int ExchangeId { get; set; } = ExternalNames.BrokerTypeToIds[BrokerType.Unknown];

    /// <summary>
    /// Any additional order parameters.
    /// </summary>
    [AsJson]
    public AdvancedOrderSettings? AdvancedSettings { get; set; }

    /// <summary>
    /// Gets if the order is successfully placed (either it is still alive or filled).
    /// </summary>
    [DatabaseIgnore]
    public bool IsSuccessful => Status is OrderStatus.Live or OrderStatus.Filled or OrderStatus.PartialFilled;

    public object Clone()
    {
        var advanced = AdvancedSettings?.Clone();
        var order = (Order)MemberwiseClone();
        order.AdvancedSettings = (AdvancedOrderSettings?)advanced;
        return order;
    }

    public int CompareTo(Order? other)
    {
        var r = ExternalOrderId.CompareTo(other?.ExternalOrderId);
        if (r != 0) r = SecurityId.CompareTo(other?.SecurityId);
        if (r != 0) r = AccountId.CompareTo(other?.AccountId);
        if (r != 0) r = ParentOrderId.CompareTo(other?.ParentOrderId);
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
        if (AdvancedSettings != null)
        {
            if (r != 0) r = AdvancedSettings.TimeInForceTime.CompareTo(other?.AdvancedSettings?.TimeInForceTime);
            if (r != 0) r = AdvancedSettings.TrailingSpread.CompareTo(other?.AdvancedSettings?.TrailingSpread);
            if (r != 0) r = AdvancedSettings.TrailingType.CompareTo(other?.AdvancedSettings?.TrailingType);
            if (r != 0) r = AdvancedSettings.TrailingValue.CompareTo(other?.AdvancedSettings?.TrailingValue);
        }
        return r;
    }

    public override string ToString()
    {
        return $"[{Id}][{ExternalOrderId}][{CreateTime:yyMMdd-HHmmss}][{Status}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}";
    }
}

public class AdvancedOrderSettings : ICloneable
{
    public OrderTrailingType TrailingType { get; set; }
    public double TrailingValue { get; set; }
    public double TrailingSpread { get; set; }
    public DateTime TimeInForceTime { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}

public enum OrderTrailingType
{
    None,
    Ratio,
    Amount,
}
