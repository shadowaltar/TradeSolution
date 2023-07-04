using System.Text.RegularExpressions;

namespace TradeDataCore.Utils;
public static class DateUtils
{
    public static bool TryParseFileSourceDate(string filePath, out DateTime asOfTime)
    {
        asOfTime = DateTime.MinValue;
        var fileName = Path.GetFileName(filePath);
        var match = Regex.Match(fileName, @"\d+");
        if (match.Success)
        {
            asOfTime = match.Value.ParseDate();
            return true;
        }
        return false;
    }

    public static DateTime? NullIfMin(this DateTime value)
    {
        return value == DateTime.MinValue ? null : value;
    }
}
