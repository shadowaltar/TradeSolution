using System.Runtime.CompilerServices;

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

    public static bool IsWeekend(this DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

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
        var oneMs = TimeSpan.FromMilliseconds(1);
        var segments = new List<(DateTime start, DateTime end)>();
        while (start < end)
        {
            var segmentEnd = Min(start + interval - oneMs, end);
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
