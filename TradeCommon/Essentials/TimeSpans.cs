namespace TradeCommon.Essentials;
public class TimeSpans
{
    public static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan FifteenSeconds = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ThirtySeconds = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    public static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    public static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);


    public static readonly TimeSpan LocalUtcDiff;
    static TimeSpans()
    {
        var utcNow = DateTime.UtcNow;
        var localNow = utcNow.ToLocalTime();
        LocalUtcDiff = localNow - utcNow;
    }
}

