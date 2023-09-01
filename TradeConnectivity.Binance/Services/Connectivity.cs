using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Services;

public class Connectivity : IExternalConnectivityManagement
{
    private const string ProdHttps = "https://api.binance.vision";
    private const string ProdWs = "wss://stream.binance.com:9443";

    private const string TestHttps = "https://testnet.binance.vision";
    private const string TestWs = "wss://testnet.binance.vision/ws";

    public string RootUrl { get; protected set; }

    public string RootWebSocketUrl { get; protected set; }

    public Connectivity()
    {
        SetEnvironment(EnvironmentType.Test);
    }

    public void SetEnvironment(EnvironmentType environment)
    {
        switch (environment)
        {
            case EnvironmentType.Test:
            case EnvironmentType.Uat:
                RootUrl = TestHttps;
                RootWebSocketUrl = TestWs;
                break;
            case EnvironmentType.Prod:
                RootUrl = ProdHttps;
                RootWebSocketUrl = ProdWs;
                break;
            default: throw new ArgumentException("Invalid environment type", nameof(environment));
        }
    }
}
