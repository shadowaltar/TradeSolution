using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// The object which records each state change of an order.
/// </summary>
[Storage("order_states", "execution")]
[Unique(nameof(Id))]
[Index(nameof(OrderId))]
[Index(nameof(Time))]
public record OrderState : SecurityRelatedEntry
{
    [DatabaseIgnore]
    private static readonly IdGenerator _orderStateIdGen = IdGenerators.Get<OrderState>();

    [NotNull, Positive]
    public long Id { get; set; }
    [NotNull]
    public long OrderId { get; set; }
    [NotNull]
    public decimal FilledQuantity { get; set; }
    [NotNull]
    public OrderStatus Status { get; set; } = OrderStatus.Unknown;
    [NotNull]
    public DateTime Time { get; set; }

    public static OrderState From(Order order)
    {
        return new OrderState
        {
            Id = _orderStateIdGen.NewTimeBasedId,
            OrderId = order.Id,
            FilledQuantity = order.FilledQuantity,
            Status = order.Status,
            Time = order.UpdateTime,
            Security = order.Security,
            SecurityId = order.SecurityId,
            SecurityCode = order.SecurityCode,
        };
    }
}
