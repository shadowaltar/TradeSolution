using TradeCommon.Essentials;

namespace TradeCommon.Integrity;
public record MissingPriceSituation(int SecurityId, DateTime StartTime, int Count, IntervalType IntervalType);

