namespace TradeCommon.Essentials;

public enum IntervalType
{
    Unknown,
    OneSecond,
    OneMinute,
    TwoMinutes,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    OneHour,
    NintyMinutes,
    FourHours,
    OneDay,
    OneWeek,
    OneMonth,
    ThreeMonths,
    OneYear,
}

public static class IntervalTypeConverter
{
    public static IntervalType Parse(string intervalStr)
    {
        if (intervalStr == null)
            return IntervalType.Unknown;

        intervalStr = intervalStr.Trim().ToUpperInvariant();
        return intervalStr switch
        {
            "1D" => IntervalType.OneDay,
            "1S" => IntervalType.OneSecond,
            "1M" or "1MIN" => IntervalType.OneMinute,
            "2M" or "2MIN" => IntervalType.TwoMinutes,
            "5M" or "5MIN" => IntervalType.FiveMinutes,
            "15M" or "15MIN" => IntervalType.FifteenMinutes,
            "30M" or "30MIN" => IntervalType.ThirtyMinutes,
            "1H" => IntervalType.OneHour,
            "90M" or "90MIN" => IntervalType.NintyMinutes,
            "4H" => IntervalType.FourHours,
            "1W" or "1WK" => IntervalType.OneWeek,
            "1MO" or "1MONTH" => IntervalType.OneMonth,
            "3MO" or "3MONTH" => IntervalType.ThreeMonths,
            "1Y" => IntervalType.OneYear,
            _ => IntervalType.Unknown,
        };
    }

    public static string ToYahooIntervalString(IntervalType interval)
    {
        return interval switch
        {
            IntervalType.OneDay => "1d",
            IntervalType.OneSecond => "",
            IntervalType.OneMinute => "1m",
            IntervalType.TwoMinutes => "2m",
            IntervalType.FiveMinutes => "5m",
            IntervalType.FifteenMinutes => "15m",
            IntervalType.ThirtyMinutes => "30m",
            IntervalType.OneHour => "1h",
            IntervalType.NintyMinutes => "90m",
            IntervalType.FourHours => "",
            IntervalType.OneWeek => "1wk",
            IntervalType.OneMonth => "1mo",
            IntervalType.ThreeMonths => "3mo",
            IntervalType.OneYear => "",
            _ => ""
        };
    }

    public static string ToIntervalString(IntervalType interval)
    {
        return interval switch
        {
            IntervalType.OneDay => "1d",
            IntervalType.OneSecond => "1s",
            IntervalType.OneMinute => "1m",
            IntervalType.TwoMinutes => "2m",
            IntervalType.FiveMinutes => "5m",
            IntervalType.FifteenMinutes => "15m",
            IntervalType.ThirtyMinutes => "30m",
            IntervalType.OneHour => "1h",
            IntervalType.NintyMinutes => "90m",
            IntervalType.FourHours => "4h",
            IntervalType.OneWeek => "1w",
            IntervalType.OneMonth => "1mo",
            IntervalType.ThreeMonths => "3mo",
            IntervalType.OneYear => "1y",
            _ => ""
        };
    }

    public static TimeSpan ToTimeSpan(IntervalType interval)
    {
        return interval switch
        {
            IntervalType.OneDay => new TimeSpan(1, 0, 0, 0),
            IntervalType.OneSecond => new TimeSpan(0, 0, 1),
            IntervalType.OneMinute => new TimeSpan(0, 1, 0),
            IntervalType.TwoMinutes => new TimeSpan(0, 2, 0),
            IntervalType.FiveMinutes => new TimeSpan(0, 5, 0),
            IntervalType.FifteenMinutes => new TimeSpan(0, 15, 0),
            IntervalType.ThirtyMinutes => new TimeSpan(0, 30, 0),
            IntervalType.OneHour => new TimeSpan(1, 0, 0),
            IntervalType.NintyMinutes => new TimeSpan(1, 30, 0),
            IntervalType.FourHours => new TimeSpan(4, 0, 0),
            IntervalType.OneWeek => new TimeSpan(7, 0, 0, 0),
            IntervalType.OneMonth => new TimeSpan(30, 0, 0, 0),
            IntervalType.ThreeMonths => new TimeSpan(90, 0, 0, 0),
            IntervalType.OneYear => new TimeSpan(365, 0, 0, 0),
            _ => TimeSpan.Zero,
        };
    }
}