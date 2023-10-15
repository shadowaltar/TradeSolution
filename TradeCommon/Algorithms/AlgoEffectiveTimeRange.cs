using TradeCommon.Essentials;
using Common;

namespace TradeCommon.Algorithms;

public record AlgoEffectiveTimeRange
{
    private DateTime? _designatedStart;
    private DateTime? _designatedStop;

    public AlgoStartTimeType WhenToStart { get; set; }

    public AlgoStopTimeType WhenToStop { get; set; }

    public DateTime? DesignatedStart
    {
        get => _designatedStart;
        set
        {
            _designatedStart = value;
            if (value != null)
                WhenToStart = AlgoStartTimeType.Designated;
        }
    }

    public DateTime? DesignatedStop
    {
        get => _designatedStop;
        set
        {
            _designatedStop = value;
            if (value != null)
                WhenToStop = AlgoStopTimeType.Designated;
        }
    }

    public int HoursBeforeMaintenance { get; set; } = 3;

    public IntervalType? NextStartOfIntervalType { get; set; }

    public DateTime ActualStartTime
    {
        get
        {
            switch (WhenToStart)
            {
                case AlgoStartTimeType.Designated:
                    return DesignatedStart == null ? DateTime.MinValue : DesignatedStart.Value;
                case AlgoStartTimeType.Immediately:
                    return DateTime.UtcNow;
                case AlgoStartTimeType.Never:
                    return DateTime.MaxValue;
                case AlgoStartTimeType.NextStartOf:
                    if (NextStartOfIntervalType != null)
                    {
                        var now = DateTime.UtcNow;
                        return now.NextOf(IntervalTypeConverter.ToTimeSpan(NextStartOfIntervalType.Value));
                    }
                    return DateTime.MinValue;
                case AlgoStartTimeType.NextStartOfLocalDay:
                    {
                        var now = DateTime.UtcNow;
                        var localNow = now.ToLocalTime();
                        return now.Add(TimeSpans.LocalUtcDiff);
                    }
                case AlgoStartTimeType.NextMarketOpens:
                    // TODO, need market meta data
                    break;
                case AlgoStartTimeType.NextWeekMarketOpens:
                    // TODO, need market meta data
                    break;
            }
            return DateTime.MinValue;
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
        IntervalType? nextStartOfIntervalType = null;
        switch (intervalType)
        {
            case IntervalType.OneMinute:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneMinute;
                break;
            case IntervalType.OneHour:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneHour;
                break;
            case IntervalType.OneDay:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneDay;
                break;
        }
        return new AlgoEffectiveTimeRange
        {
            WhenToStart = algoStartTimeType,
            WhenToStop = AlgoStopTimeType.Never,
            NextStartOfIntervalType = nextStartOfIntervalType,
        };
    }

    public static AlgoEffectiveTimeRange ForProduction(IntervalType intervalType)
    {
        AlgoStartTimeType algoStartTimeType = AlgoStartTimeType.Never;
        IntervalType? nextStartOfIntervalType = null;
        switch (intervalType)
        {
            case IntervalType.OneMinute:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneMinute;
                break;
            case IntervalType.OneHour:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneHour;
                break;
            case IntervalType.OneDay:
                algoStartTimeType = AlgoStartTimeType.NextStartOf;
                nextStartOfIntervalType = IntervalType.OneDay;
                break;
            default:
                // TODO
                throw new NotImplementedException();
        }
        return new AlgoEffectiveTimeRange
        {
            WhenToStart = algoStartTimeType,
            WhenToStop = AlgoStopTimeType.BeforeBrokerMaintenance,
            HoursBeforeMaintenance = 3,
            NextStartOfIntervalType = nextStartOfIntervalType,
        };
    }
}