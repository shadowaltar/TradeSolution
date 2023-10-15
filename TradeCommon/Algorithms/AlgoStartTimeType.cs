namespace TradeCommon.Algorithms;

public enum AlgoStartTimeType
{
    Never,
    Immediately,
    Designated,
    NextStartOf,
    NextStartOfLocalDay,
    NextMarketOpens,
    NextWeekMarketOpens,
}
