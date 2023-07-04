using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TradeDataCore.Utils;

public static class StringExtensions
{
    /// <summary>
    /// Returns true if <paramref name="value"/> is null or whitespace.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsBlank([NotNullWhen(false)][AllowNull] this string value) => string.IsNullOrWhiteSpace(value);

    public static DateTime ParseDate(this string value, string? format = "yyyyMMdd", DateTime defaultValue = default) =>
        DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : defaultValue;

    public static long ParseLong(this string? value, long defaultValue = default, string cultureInfoName = "")
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (long.TryParse(value,
            string.IsNullOrEmpty(cultureInfoName) ? CultureInfo.InvariantCulture :
            CultureInfo.GetCultureInfo(cultureInfoName), out long result))
            return result;
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
        return defaultValue;
    }

    public static bool ParseBool(this string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
