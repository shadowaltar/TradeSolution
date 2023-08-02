using System.Security.Cryptography;
using System.Text;

namespace Common;

public static class CryptographyUtils
{
    public static string Encrypt(string text, string salt = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var inputs = Encoding.UTF8.GetBytes(text + salt);
        var outputs = SHA512.HashData(inputs);

        return BitConverter.ToString(outputs).Replace("-", string.Empty);
    }
}
