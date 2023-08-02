using Common;
using System.Security.Cryptography;
using System.Text;
using TradeCommon.Essentials;

namespace TradeConnectivity.Binance.Services;
public class KeyManager
{
    public string ApiKey { get; private set; } = "gz2ZlwFL6equlgho6rpv05xDE3LvLZ4IAx5iVLSRmcdY1ourA8IJoZBTT5iH47Nx";
    public string SecretKey { get; private set; } = "oc58o7h4qTNI8BBPHnCUJW1SJ1kiw3qYSFx5j4QGYoufb68lHYIWdqBzFwwpJbrn";
    public HMACSHA256? Hasher { get; private set; }

    /// <summary>
    /// Select current user and use its credentials / keys.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public bool Select(User user)
    {
        // TODO support multi-tenant        
        Hasher?.Dispose();

        if (SecretKey.IsBlank())
            SecretKey = File.ReadAllText(@"C:\Vault\secret.txt");

        var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
        Hasher = new HMACSHA256(keyBytes);
        
        return true;
    }
}

