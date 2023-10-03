using Common.Attributes;
using TradeCommon.Database;

namespace TradeCommon.Essentials.Misc;

[Storage("position_reconciliation", DatabaseNames.ExecutionData)]
[Unique(nameof(LastPositionId), nameof(SecurityId))]
[Index(nameof(EndTime))]
public record PositionReconciliationRecord
{
    public int SecurityId { get; set; }

    /// <summary>
    /// If it is zero, it means the position (of a specific security)
    /// is closed, otherwise there is an open position.
    /// </summary>
    public long LastPositionId { get; set; }

    /// <summary>
    /// The last trade's update time or the position's close time.
    /// </summary>
    public DateTime EndTime { get; set; }
}
