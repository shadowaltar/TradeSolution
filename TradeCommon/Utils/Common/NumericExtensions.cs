namespace Common;
public static class NumericExtensions
{
    public static decimal? NullIfZero(this decimal value)
    {
        return value == 0 ? null : value;
    }

    public static decimal? NullIfInvalid(this decimal value)
    {
        return !value.IsValid() ? null : value;
    }

    public static long? NullIfInvalid(this long value)
    {
        return !value.IsValid() ? null : value;
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

    public static int GetDecimalPlaces(this decimal n)
    {
        n = Math.Abs(n); //make sure it is positive.
        n -= (int)n;     //remove the integer part of the number.
        var decimalPlaces = 0;
        while (n > 0)
        {
            decimalPlaces++;
            n *= 10;
            n -= (int)n;
        }
        return decimalPlaces;
    }

    public static bool IsNaN(this double value) => double.IsNaN(value);

    public static bool IsValid(this int value) => value != int.MinValue && value != int.MaxValue;

    public static bool IsValid(this double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static bool IsValid(this decimal value) => value != decimal.MinValue && value != decimal.MaxValue;

    public static bool IsValid(this decimal? value) => value != null && value != decimal.MinValue && value != decimal.MaxValue;

    public static bool IsValid(this long value) => value != long.MinValue && value != long.MaxValue;

    public static bool ApproxEquals(this decimal value1, decimal value2)
    {
        var diff = value1 - value2;
        if (diff == 0) return true;
        if (diff > 0 && diff < 1E-20m) return true;
        if (diff < 0 && diff > -1E-20m) return true;
        return false;
    }

    public static bool ApproxEquals(this double value1, double value2)
    {
        var diff = value1 - value2;
        if (diff == 0) return true;
        if (diff > 0 && diff < 1E-14) return true;
        if (diff < 0 && diff > -1E-14) return true;
        return false;
    }

    public static double ToDouble(this decimal value) => decimal.ToDouble(value);

    public static string NAIfInvalid(this decimal value, string? format = null) => value.IsValid() ? (format != null ? value.ToString(format) : value.ToString()) : "N/A";
}
