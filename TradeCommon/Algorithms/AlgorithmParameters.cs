using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Algorithms;

public record AlgorithmParameters(bool IsBackTesting,
                                  IntervalType Interval,
                                  List<Security> SecurityPool,
                                  AlgoEffectiveTimeRange TimeRange)
{
    public bool ShouldCloseOpenPositionsWhenHalted { get; set; } = true;
}