﻿using Common;

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
    public static TimeRangeType Parse(string? strInterval)
    {
        if (strInterval.IsBlank())
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
        return range switch
        {
            TimeRangeType.OneDay => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddDays(1),
                    OperatorType.Minus => input.AddDays(-1),
                    _ => input
                };
            }),
            TimeRangeType.OneWeek => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddDays(7),
                    OperatorType.Minus => input.AddDays(-7),
                    _ => input
                };
            }),
            TimeRangeType.OneMonth => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddMonths(1),
                    OperatorType.Minus => input.AddMonths(-1),
                    _ => input
                };
            }),
            TimeRangeType.SixMonths => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddMonths(6),
                    OperatorType.Minus => input.AddMonths(-6),
                    _ => input
                };
            }),
            TimeRangeType.OneYear => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddYears(1),
                    OperatorType.Minus => input.AddYears(-1),
                    _ => input
                };
            }),
            TimeRangeType.TwoYears => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddYears(2),
                    OperatorType.Minus => input.AddYears(-2),
                    _ => input
                };
            }),
            TimeRangeType.ThreeYears => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddYears(3),
                    OperatorType.Minus => input.AddYears(-3),
                    _ => input
                };
            }),
            TimeRangeType.TenYears => new Func<DateTime, DateTime>(input =>
            {
                return op switch
                {
                    OperatorType.Plus => input.AddYears(10),
                    OperatorType.Minus => input.AddYears(-10),
                    _ => input
                };
            }),
            TimeRangeType.YearToDay => new Func<DateTime, DateTime>(input => new DateTime(input.Year, 1, 1)),
            TimeRangeType.Max => new Func<DateTime, DateTime>(_ => new DateTime(1970, 1, 1)),
            _ => new Func<DateTime, DateTime>(_ => DateTime.MaxValue),
        };
        ;
    }
}