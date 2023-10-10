using Common.Attributes;
using TradeCommon.Database;

namespace TradeCommon.Essentials.Misc;

[Storage("position_records", DatabaseNames.ExecutionData)]
[Unique(nameof(SecurityId))]
public record PositionRecord
{
    public int SecurityId { get; set; }

    /// <summary>
    /// If it is zero, it means the position (of a specific security)
    /// is closed, otherwise there is an open position.
    /// </summary>
    public long PositionId { get; set; }

    /// <summary>
    /// Indicates if the position is closed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Total count of trades for given position.
    /// </summary>
    public int TradeCount { get; set; }

    /// <summary>
    /// The last trade's update time or the position's close time.
    /// </summary>
    public DateTime EndTime { get; set; }
}
