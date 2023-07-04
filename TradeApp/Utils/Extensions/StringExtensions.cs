using System;
using System.Globalization;

namespace TradeApp.Utils.Extensions;

public static class StringExtensions
{
    public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

    public static DateTime ParseDate(this string value, string format = "yyyyMMdd", DateTime defaultValue = default) =>
        DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : defaultValue;

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
