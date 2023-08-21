using TradeCommon.Essentials;

namespace TradeLogicCore.Algorithms.Parameters;

public record AlgoEffectiveTimeRange
{
    public required AlgoStartTimeType WhenToStart { get; set; }
    public required AlgoStopTimeType WhenToStop { get; set; }
    public DateTime? DesignatedStart { get; set; }
    public DateTime? DesignatedStop { get; set; }
    public int HoursBeforeMaintenance { get; set; } = 3;

    public static AlgoEffectiveTimeRange ForBackTesting(DateTime start, DateTime end)
    {
        return new AlgoEffectiveTimeRange
        {
            DesignatedStart = start,
            DesignatedStop = end,
            WhenToStart = AlgoStartTimeType.Designated,
            WhenToStop = AlgoStopTimeType.Designated,
        };
    }

    public static AlgoEffectiveTimeRange ForPaperTrading(IntervalType intervalType)
    {
        AlgoStartTimeType algoStartTimeType = AlgoStartTimeType.Never;
        switch (intervalType)
        {
            case IntervalType.OneMinute:
                algoStartTimeType = AlgoStartTimeType.NextStartOfMinute; break;
            case IntervalType.OneHour:
                algoStartTimeType = AlgoStartTimeType.NextStartOfHour; break;
            case IntervalType.OneDay:
                algoStartTimeType = AlgoStartTimeType.NextStartOfUtcDay; break;
        }
        return new AlgoEffectiveTimeRange
        {
            WhenToStart = algoStartTimeType,
            WhenToStop = AlgoStopTimeType.Never,
        };
    }

    public static AlgoEffectiveTimeRange ForProduction(IntervalType intervalType)
    {
        AlgoStartTimeType algoStartTimeType = AlgoStartTimeType.Never;
        switch (intervalType)
        {
            case IntervalType.OneMinute:
                algoStartTimeType = AlgoStartTimeType.NextStartOfMinute; break;
            case IntervalType.OneHour:
                algoStartTimeType = AlgoStartTimeType.NextStartOfHour; break;
            case IntervalType.OneDay:
                algoStartTimeType = AlgoStartTimeType.NextStartOfUtcDay; break;
        }
        return new AlgoEffectiveTimeRange
        {
            WhenToStart = algoStartTimeType,
            WhenToStop = AlgoStopTimeType.BeforeBrokerMaintenance,
            HoursBeforeMaintenance = 3,
        };
    }
}