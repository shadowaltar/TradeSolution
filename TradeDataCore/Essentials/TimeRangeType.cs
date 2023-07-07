namespace TradeDataCore.Essentials;

public enum TimeRangeType
{
    Unknown,
    OneDay,
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
            "1MO" or "1MONTH" => TimeRangeType.OneMonth,
            "6MO" or "6MONTH" => TimeRangeType.SixMonths,
            "1Y" => TimeRangeType.OneYear,
            "2Y" => TimeRangeType.TwoYears,
            "3Y" => TimeRangeType.ThreeYears,
            "10Y" => TimeRangeType.TenYears,
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
}