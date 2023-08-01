using TradeCommon.Runtime;

namespace TradeConnectivity.Binance;
public static class RootUrls
{
    public static string DefaultHttps { get; private set; }
    public static string DefaultWs { get; private set; }

    public static readonly string ProdHttps = "https://api.binance.vision";
    public static readonly string ProdWs = "wss://stream.binance.com:9443";

    private const string TestHttps = "https://testnet.binance.vision";
    private const string TestWs = "wss://testnet.binance.vision/ws";

    static RootUrls()
    {
        SetEnvironment(EnvironmentType.Test);
    }

    public static void SetEnvironment(EnvironmentType environment)
    {
        switch (environment)
        {
            case EnvironmentType.Test:
            case EnvironmentType.Uat:
                DefaultHttps = TestHttps;
                DefaultWs = TestWs;
                break;
            case EnvironmentType.Prod:
                DefaultHttps = ProdHttps;
                DefaultWs = ProdWs;
                break;
            default: throw new ArgumentException(nameof(environment));
        }
    }
}
