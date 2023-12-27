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
            reverse = (reverse * 10) + rem;
            value /= 10;
        }
        return reverse;
    }

    public static int GetDecimalPlaces(this decimal n)
    {
        n = Math.Abs(n); // make sure it is positive.
        n -= (int)n;     // remove the integer part of the number.
        var decimalPlaces = 0;
        while (n > 0)
        {
            decimalPlaces++;
            n *= 10;
            n -= (int)n;
        }
        return decimalPlaces;
    }

    public static int GetSignificantDigits(this long value)
    {
        int significantDigits = 0;
        long tempValue = Math.Abs(value);

        while (tempValue != 0)
        {
            tempValue /= 10;
            significantDigits++;
        }

        return significantDigits;
    }

    public static bool IsNaN(this double value)
    {
        return double.IsNaN(value);
    }

    public static bool IsValid(this int value)
    {
        return value is not int.MinValue and not int.MaxValue;
    }

    public static bool IsValid(this double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static bool IsValid(this decimal value)
    {
        return value is not decimal.MinValue and not decimal.MaxValue;
    }

    public static bool IsValid(this decimal? value)
    {
        return value is not null and not decimal.MinValue and not decimal.MaxValue;
    }

    public static bool IsValid(this long value)
    {
        return value is not long.MinValue and not long.MaxValue;
    }

    public static bool ApproxEquals(this decimal value1, decimal value2)
    {
        var diff = value1 - value2;
        return diff is 0 or (> 0 and < 1E-20m) or (< 0 and > (-1E-20m));
    }

    public static bool ApproxEquals(this double value1, double value2)
    {
        var diff = value1 - value2;
        return diff is 0 or (> 0 and < 1E-14) or (< 0 and > (-1E-14));
    }

    public static double ToDouble(this decimal value)
    {
        return decimal.ToDouble(value);
    }

    public static double ToDouble(this string value)
    {
        return double.TryParse(value, out var val) ? val : double.NaN;
    }

    public static decimal ToDecimal(this string value)
    {
        return decimal.TryParse(value, out var val) ? val : decimal.MinValue;
    }

    public static string NAIfInvalid(this decimal value, string? format = null)
    {
        return value.IsValid() ? (format != null ? value.ToString(format) : value.ToString()) : "N/A";
    }
}
