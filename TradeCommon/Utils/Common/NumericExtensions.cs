namespace Common;
public static class NumericExtensions
{
    public static decimal? NullIfZero(this decimal value)
    {
        return value == 0 ? null : value;
    }

    public static long ReverseDigits(this long value)
    {
        long reverse = 0;
        long rem;
        while (value != 0)
        {
            rem = value % 10;
            reverse = reverse * 10 + rem;
            value /= 10;
        }
        return reverse;
    }

    public static bool IsNaN(this double value) => double.IsNaN(value);

    public static bool IsValid(this double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static bool IsValid(this decimal value) => value != decimal.MaxValue && value != decimal.MinValue;

    public static bool ApproxEquals(this decimal value1, decimal value2)
    {
        var diff = value1 - value2;
        if (diff == 0) return true;
        if (diff > 0 && diff < 0.00000000000000000001m) return true;
        if (diff < 0 && diff > -0.00000000000000000001m) return true;
        return false;
    }
}
