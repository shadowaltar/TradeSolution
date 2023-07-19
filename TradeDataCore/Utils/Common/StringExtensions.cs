using Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Common;

public static class StringExtensions
{
    /// <summary>
    /// Returns true if <paramref name="value"/> is null or whitespace.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsBlank([NotNullWhen(false)][AllowNull] this string value) => string.IsNullOrWhiteSpace(value);

    public static bool ContainsIgnoreCase(this string @string, string subString)
    {
        if (@string == null) throw new ArgumentNullException(nameof(@string));
        if (subString == null) throw new ArgumentNullException(nameof(subString));
        return @string.IndexOf(subString, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool ContainsIgnoreCase(this IList<string> strings, string? value)
    {
        if (strings == null) throw new ArgumentNullException(nameof(strings));

        for (int i = 0; i < strings.Count; i++)
        {
            if (strings[i].Equals(value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static DateTime ParseDate(this string? value, string? format = "yyyyMMdd", DateTime defaultValue = default)
    {
        if (value.IsBlank())
        {
            return defaultValue;
        }
        return DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : defaultValue;
    }

    public static DateTime ParseLocalUnixDate(this string? value, DateTime defaultValue = default)
    {
        if (!value.IsBlank() && int.TryParse(value, CultureInfo.InvariantCulture, out var seconds))
        {
            return DateUtils.FromLocalUnixSec(seconds);
        }
        return defaultValue;
    }

    public static string ParseString(this object? obj, string defaultValue = "", bool shouldTrim = true)
    {
        var v = obj?.ToString() ?? defaultValue;
        return shouldTrim ? v.Trim() : v;
    }

    public static long ParseLong(this string? value, long defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (long.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out long result))
            return result;

        value = value.ToUpperInvariant();
        if (value.EndsWith("M"))
        {
            if (long.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000_000L;
            }
        }
        if (value.EndsWith("K"))
        {
            if (long.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000L;
            }
        }
        if (value.EndsWith("G") || value.EndsWith("B"))
        {
            if (long.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000_000_000L;
            }
        }

        return defaultValue;
    }

    public static double ParseDouble(this string? value, double defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (double.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out double result))
            return result;
        return defaultValue;
    }

    public static decimal ParseDecimal(this string? value, decimal defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (decimal.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out decimal result))
            return result;
        return defaultValue;
    }

    public static decimal ParsePercentage(this string? value, decimal defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        var isPercentage = true;
        if (!value.EndsWith("%", StringComparison.Ordinal))
            isPercentage = false;

        if (decimal.TryParse(isPercentage ? value[..(value.Length - 1)] : value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out decimal result))
        {
            return isPercentage ? result / 100m : result;
        }
        return defaultValue;
    }

    public static int ParseInt(this string? value, int defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (int.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out int result))
            return result;

        value = value.ToUpperInvariant();
        if (value.EndsWith("M"))
        {
            if (int.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000_000;
            }
        }
        if (value.EndsWith("K"))
        {
            if (int.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000;
            }
        }
        if (value.EndsWith("G") || value.EndsWith("B"))
        {
            if (int.TryParse(value[..(value.Length - 1)], out var result2))
            {
                return result2 * 1_000_000_000;
            }
        }

        return defaultValue;
    }

    public static bool ParseBool(this string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
