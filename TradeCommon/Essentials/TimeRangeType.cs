namespace TradeDataCore.Essentials;

public enum TimeRangeType
{
    Unknown,
    OneDay,
    OneWeek,
    OneMonth,
    SixMonths,
    OneYear,
    TwoYears,
    ThreeYears,
    TenYears,
    YearToDay,
    Max,
}


public static class TimeRangeTypeConverter
{
    public static TimeRangeType Parse(string strInterval)
    {
        if (strInterval == null)
            return TimeRangeType.Unknown;

        strInterval = strInterval.Trim().ToUpperInvariant();
        return strInterval switch
        {
            "1D" => TimeRangeType.OneDay,
            "1W" => TimeRangeType.OneWeek,
            "1MO" or "1MONTH" => TimeRangeType.OneMonth,
            "6MO" or "6MONTH" => TimeRangeType.SixMonths,
            "1Y" or "1YR" => TimeRangeType.OneYear,
            "2Y" or "2YR" => TimeRangeType.TwoYears,
            "3Y" or "3YR" => TimeRangeType.ThreeYears,
            "10Y" or "10YR" => TimeRangeType.TenYears,
            "YTD" => TimeRangeType.YearToDay,
            "MAX" => TimeRangeType.Max,
            _ => TimeRangeType.Unknown,
        };
    }

    public static string ToYahooIntervalString(TimeRangeType interval)
    {
        return interval switch
        {
            TimeRangeType.OneDay => "1d",
            TimeRangeType.OneWeek => "1w",
            TimeRangeType.OneMonth => "1mo",
            TimeRangeType.SixMonths => "6mo",
            TimeRangeType.OneYear => "1y",
            TimeRangeType.TwoYears => "2y",
            TimeRangeType.ThreeYears => "3y",
            TimeRangeType.TenYears => "10y",
            TimeRangeType.YearToDay => "ytd",
            TimeRangeType.Max => "max",
            _ => ""
        };
    }

    public static Func<DateTime, DateTime> ConvertTimeSpan(TimeRangeType range, OperatorType op)
    {
        switch (range)
        {
            case TimeRangeType.OneDay:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddDays(1),
                        OperatorType.Minus => input.AddDays(-1),
                        _ => input
                    };
                });
            case TimeRangeType.OneWeek:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddDays(7),
                        OperatorType.Minus => input.AddDays(-7),
                        _ => input
                    };
                });
            case TimeRangeType.OneMonth:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddMonths(1),
                        OperatorType.Minus => input.AddMonths(-1),
                        _ => input
                    };
                });
            case TimeRangeType.SixMonths:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddMonths(6),
                        OperatorType.Minus => input.AddMonths(-6),
                        _ => input
                    };
                });
            case TimeRangeType.OneYear:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddYears(1),
                        OperatorType.Minus => input.AddYears(-1),
                        _ => input
                    };
                });
            case TimeRangeType.TwoYears:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddYears(2),
                        OperatorType.Minus => input.AddYears(-2),
                        _ => input
                    };
                });
            case TimeRangeType.ThreeYears:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddYears(3),
                        OperatorType.Minus => input.AddYears(-3),
                        _ => input
                    };
                });
            case TimeRangeType.TenYears:
                return new Func<DateTime, DateTime>(input =>
                {
                    return op switch
                    {
                        OperatorType.Plus => input.AddYears(10),
                        OperatorType.Minus => input.AddYears(-10),
                        _ => input
                    };
                });
            case TimeRangeType.YearToDay:
                return new Func<DateTime, DateTime>(input => new DateTime(input.Year, 1, 1));
            case TimeRangeType.Max:
                return new Func<DateTime, DateTime>(_ => new DateTime(1970, 1, 1));
            case TimeRangeType.Unknown:
            default:
                return new Func<DateTime, DateTime>(_ => DateTime.MaxValue);
        };
    }
}