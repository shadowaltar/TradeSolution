using System.Runtime.CompilerServices;
using TradeCommon.Essentials;

namespace Common;
public static class DateUtils
{
    public static int ToUnixSec(this DateTime value)
    {
        return (int)value.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }

    public static long ToUnixMs(this DateTime value)
    {
        return (long)value.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
    }

    public static DateTime FromLocalUnixSec(this long unixSec)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSec).ToLocalTime().DateTime;
    }

    public static DateTime FromLocalUnixMs(this long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().DateTime;
    }

    public static DateTime FromUnixSec(this int unixSec)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSec).DateTime;
    }

    public static DateTime FromUnixMs(this long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).DateTime;
    }

    public static DateTime? NullIfMin(this DateTime value)
    {
        return value == DateTime.MinValue ? null : value;
    }

    public static DateTime AddBusinessDays(this DateTime current,
                                           int days,
                                           IList<DateTime>? holidays = null)
    {
        var sign = Math.Sign(days);
        var unsignedDays = Math.Abs(days);
        for (var i = 0; i < unsignedDays; i++)
        {
            do
            {
                current = current.AddDays(sign);
            }
            while (current.DayOfWeek == DayOfWeek.Saturday
                || current.DayOfWeek == DayOfWeek.Sunday
                || (holidays != null && holidays.Contains(current.Date)));
        }
        return current;
    }

    public static bool IsValid(this DateTime value) => value != DateTime.MaxValue && value != DateTime.MinValue;
    
    public static bool IsValid(this DateTime? value) => value != null && value != DateTime.MaxValue && value != DateTime.MinValue;

    public static bool IsWeekend(this DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static DateTime NextOf(this DateTime fromTime, TimeSpan interval)
    {
        DateTime baseTime;
        if (interval == TimeSpans.OneMinute)
        {
            if (fromTime.Second == 0)
                baseTime = fromTime;
            else
                baseTime = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, fromTime.Minute, 0);
            return baseTime.AddMinutes(1);
        }
        else if (interval == TimeSpans.OneHour)
        {
            if (fromTime.Minute == 0)
                baseTime = fromTime;
            else
                baseTime = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, 0, 0);
            return baseTime.AddHours(1);
        }
        else if (interval == TimeSpans.OneDay)
        {
            if (fromTime.Hour == 0)
                baseTime = fromTime;
            else
                baseTime = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day);
            return baseTime.AddDays(1);
        }
        else if (interval == TimeSpans.OneWeek)
        {
            // find the next Monday 00:00:00
            var i = (int)fromTime.DayOfWeek;
            var add = 8 - i; // Monday == 1, +7 -> next Monday; Tuesday == 2, +6 -> next Tuesday...
            return new DateTime(fromTime.Year, fromTime.Month, fromTime.Day).AddDays(add);
        }
        throw new NotSupportedException("Only supports interval of 1m, 1h, 1d and 1w.");
    }

    public static DateTime Min(DateTime date1, DateTime date2)
    {
        return date1 < date2 ? date1 : date2;
    }

    /// <summary>
    /// Create series of DateTime with equal gap in between.
    /// The last output can be either equal to or smaller than the
    /// specified <paramref name="end"/> time.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public static List<(DateTime start, DateTime end)> CreateEqualLengthTimeIntervals(DateTime start, DateTime end, TimeSpan interval)
    {
        var segments = new List<(DateTime start, DateTime end)>();
        while (start < end)
        {
            var segmentEnd = Min(start + interval - TimeSpans.OneMillisecond, end);
            var segment = (start, segmentEnd);
            segments.Add(segment);
            start += interval;
        }
        return segments;
    }

    /// <summary>
    /// Create series of DateTime with equal gap in between.
    /// The given <paramref name="start"/> time is excluded.
    /// The last output can be either equal to or smaller than the
    /// specified <paramref name="end"/> time.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public static List<DateTime> CreateEqualGapTimePoints(DateTime start, DateTime end, TimeSpan interval, bool endInclusive = true)
    {
        var results = new List<DateTime>();
        var nextEnd = start + interval;
        while (endInclusive ? (nextEnd <= end) : (nextEnd < end))
        {
            results.Add(nextEnd);
            nextEnd += interval;
        }
        return results;
    }
}
