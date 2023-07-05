namespace TradeDataCore.Utils;
public static class DateUtils
{
    public static DateTime Max { get; } = new DateTime(9999, 1, 1);

    public static int ToUnixSec(this DateTime value)
    {
        return (int)value.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }

    public static long ToUnixMs(this DateTime value)
    {
        return (long)value.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
    }

    public static DateTime? NullIfMin(this DateTime value)
    {
        return value == DateTime.MinValue ? null : value;
    }
}
