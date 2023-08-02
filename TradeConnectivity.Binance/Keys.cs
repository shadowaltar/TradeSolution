using System.Security.Cryptography;
using System.Text;
using TradeCommon.Essentials;

namespace TradeConnectivity.Binance;
public static class Keys
{
    public static string ApiKey { get; set; } = "gz2ZlwFL6equlgho6rpv05xDE3LvLZ4IAx5iVLSRmcdY1ourA8IJoZBTT5iH47Nx";
    public static string SecretKey { get; set; } = "oc58o7h4qTNI8BBPHnCUJW1SJ1kiw3qYSFx5j4QGYoufb68lHYIWdqBzFwwpJbrn";
    public static HMACSHA256 Hasher { get; private set; }

    //public static string ApiKey { get; private set; } = "gz2ZlwFL6equlgho6rpv05xDE3LvLZ4IAx5iVLSRmcdY1ourA8IJoZBTT5iH47Nx";
    //public static string SecretKey { get; private set; } = "oc58o7h4qTNI8BBPHnCUJW1SJ1kiw3qYSFx5j4QGYoufb68lHYIWdqBzFwwpJbrn";

    public static void Initialize()
    {
        var key = File.ReadAllText(@"C:\Vault\secret.txt");
        SecretKey = key;

        var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
        Hasher = new HMACSHA256(keyBytes);
    }
}
