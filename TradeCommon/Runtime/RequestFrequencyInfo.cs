using TradeCommon.Essentials;

namespace TradeCommon.Runtime;

public record RequestFrequencyInfo(string Type, IntervalType Interval, int IntervalCount, int CurrentUsedQuota, int MaxQuota);
