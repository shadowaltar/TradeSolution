using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using TradeCommon.Constants;

namespace Common;

public static class StringExtensions
{
    /// <summary>
    /// Returns true if <paramref name="value"/> is null or whitespace.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsBlank([NotNullWhen(false)][AllowNull] this string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    public static bool EqualsIgnoreCase(this string @string, string another)
    {
        return @string == null ? Equals(@string, another) : @string.Equals(another, StringComparison.OrdinalIgnoreCase);
    }

    public static bool StartsWithIgnoreCase(this string @string, string subString)
    {
        if (@string == null) throw new ArgumentNullException(nameof(@string));
        return subString == null
            ? throw new ArgumentNullException(nameof(subString))
            : @string.StartsWith(subString, StringComparison.OrdinalIgnoreCase);
    }

    public static bool EndsWithIgnoreCase(this string @string, string subString)
    {
        if (@string == null) throw new ArgumentNullException(nameof(@string));
        return subString == null
            ? throw new ArgumentNullException(nameof(subString))
            : @string.EndsWith(subString, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsIgnoreCase(this string @string, string subString)
    {
        if (@string == null) throw new ArgumentNullException(nameof(@string));
        return subString == null
            ? throw new ArgumentNullException(nameof(subString))
            : @string.IndexOf(subString, StringComparison.OrdinalIgnoreCase) >= 0;
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

    public static DateTime ParseDate(this string? value, string? format = Constants.DefaultDateFormat, DateTime defaultValue = default)
    {
        return value.IsBlank()
            ? defaultValue
            : DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : defaultValue;
    }

    public static DateTime ParseDateOrTime(this string? value,
                                           string? dateFormat = Constants.DefaultDateFormat,
                                           string? timeFormat = Constants.DefaultDateTimeFormat,
                                           DateTime defaultValue = default)
    {
        if (value.IsBlank())
            return defaultValue;

        if (DateTime.TryParseExact(value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        else if (DateTime.TryParseExact(value, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return dateTime;
        }
        return defaultValue;
    }

    public static DateTime ParseLocalUnixDate(this string? value, DateTime defaultValue = default)
    {
        return !value.IsBlank() && long.TryParse(value, CultureInfo.InvariantCulture, out var seconds)
            ? seconds.FromLocalUnixSec()
            : defaultValue;
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
                return result2 * 1_000_000L;
        }
        if (value.EndsWith("K"))
        {
            if (long.TryParse(value[..(value.Length - 1)], out var result2))
                return result2 * 1_000L;
        }
        if (value.EndsWith("G") || value.EndsWith("B"))
        {
            if (long.TryParse(value[..(value.Length - 1)], out var result2))
                return result2 * 1_000_000_000L;
        }

        return defaultValue;
    }

    public static double ParseDouble(this string? value, double defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return double.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out double result)
            ? result
            : defaultValue;
    }

    public static decimal ParseDecimal(this string? value, int precision = int.MaxValue, decimal defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        var r = decimal.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out decimal result)
            ? result
            : defaultValue;
        if (r != defaultValue && precision!= int.MaxValue)
            r = decimal.Round(r, precision);
        return r;
    }

    public static decimal ParsePercentage(this string? value, decimal defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        var isPercentage = true;
        if (!value.EndsWith("%", StringComparison.Ordinal))
            isPercentage = false;

        return decimal.TryParse(isPercentage ? value[..(value.Length - 1)] : value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out decimal result)
            ? isPercentage ? result / 100m : result
            : defaultValue;
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
                return result2 * 1_000_000;
        }
        if (value.EndsWith("K"))
        {
            if (int.TryParse(value[..(value.Length - 1)], out var result2))
                return result2 * 1_000;
        }
        if (value.EndsWith("G") || value.EndsWith("B"))
        {
            if (int.TryParse(value[..(value.Length - 1)], out var result2))
                return result2 * 1_000_000_000;
        }

        return defaultValue;
    }

    public static bool ParseBool(this string? value, bool defaultValue = false)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public static bool IsValidEmail(this string email)
    {
        if (email.IsBlank()) return false;

        var trimmedEmail = email.Trim();

        if (trimmedEmail.EndsWith(".") || trimmedEmail.Contains(' '))
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var addr) && addr.Address == trimmedEmail;
    }

    public static StringBuilder RemoveLast(this StringBuilder sb)
    {
        if (sb.Length > 0)
            return sb.Remove(sb.Length - 2, 1);
        return sb;
    }
}
