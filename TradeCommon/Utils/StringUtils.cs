using Common;
using System.Text;

namespace TradeCommon.Utils;
public static class StringUtils
{
    public static string ToUrlParamString(List<(string, string)>? data)
    {
        if (data == null) return "";

        var sb = new StringBuilder();
        foreach (var (key, value) in data)
        {
            sb.Append(key).Append('=').Append(value).Append('&');
        }
        sb.RemoveLast();
        return sb.ToString();
    }
}
