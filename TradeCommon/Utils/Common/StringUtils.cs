using System.Text;

namespace TradeCommon.Utils.Common;
public static class StringUtils
{
    public static string ToUrlParamString(Dictionary<string, string> data)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in data)
        {
            sb.Append(key).Append('=').Append(value).Append('&');
        }
        sb.Remove(sb.Length - 1, 1);
        return sb.ToString();
    }
}
