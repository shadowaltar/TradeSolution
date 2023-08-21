using TradeCommon.Essentials;

namespace TradeLogicCore.Algorithms.Parameters;

public record AlgoEffectiveTimeRange
{
    public required AlgoStartTimeType WhenToStart { get; set; }
    public required AlgoStopTimeType WhenToStop { get; set; }
    public DateTime? DesignatedStart { get; set; }
    public DateTime? DesignatedStop { get; set; }
    public int HoursBeforeMaintenance { get; set; } = 3;

    public DateTime ActualStartTime
    {
        get
        {
            switch (WhenToStart)
            {
                case AlgoStartTimeType.Immediately:
                    return DateTime.UtcNow;
                case AlgoStartTimeType.Designated:
                    return DesignatedStart == null ? DateTime.MinValue : DesignatedStart.Value;
                case AlgoStartTimeType.NextStartOfMinute:
                {
                    var now = DateTime.UtcNow;
                    return now.Date.AddHours(now.Hour).AddMinutes(now.Minute).AddMinutes(1);
                }
                case AlgoStartTimeType.NextStartOfHour:
                {
                    var now = DateTime.UtcNow;
                    return now.Date.AddHours(now.Hour).AddHours(1);
                }
                case AlgoStartTimeType.NextStartOfUtcDay:
                {
                    var now = DateTime.UtcNow;
                    return now.Date.AddDays(1);
                }
                case AlgoStartTimeType.NextStartOfMonth:
                {
                    var now = DateTime.UtcNow;
                    return new DateTime(now.Year, now.Month, 1).AddMonths(1);
                }
                default: throw new ArgumentException();
            }
        }
    }

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