using System.Security.Cryptography;
using System.Text;

namespace TradeCommon.Utils.Common;

public static class CryptographyUtils
{
    public static string HashString(string text, string salt = "")
    {
        if (String.IsNullOrEmpty(text))
        {
            return String.Empty;
        }
        var inputs = Encoding.UTF8.GetBytes(text + salt);
        var outputs = SHA512.HashData(inputs);

        return BitConverter.ToString(outputs).Replace("-", String.Empty);
    }
}
