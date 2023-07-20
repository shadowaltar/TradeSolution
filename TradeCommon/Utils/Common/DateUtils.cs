﻿namespace Common;
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

    public static DateTime FromLocalUnixSec(int unixSec)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSec).ToLocalTime().DateTime;
    }

    public static DateTime FromLocalUnixMs(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().DateTime;
    }

    public static DateTime FromUnixSec(int unixSec)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSec).DateTime;
    }

    public static DateTime FromUnixMs(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).DateTime;
    }

    public static DateTime? NullIfMin(this DateTime value)
    {
        return value == DateTime.MinValue ? null : value;
    }
}