using System.Net.Http;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Services;

public class Connectivity : IExternalConnectivityManagement
{
    private string ProdHttps = "https://api.binance.vision";
    private string ProdWs = "wss://stream.binance.com:9443";

    private string TestHttps = "https://testnet.binance.vision";
    private string TestWs = "wss://testnet.binance.vision";

    public string RootUrl { get; protected set; }

    public string RootWebSocketUrl { get; protected set; }

    public Connectivity(ApplicationContext context)
    {
        SetEnvironment(EnvironmentType.Test);

        if (context.IsExternalProhibited)
        {
            ProdHttps = "https://not.exist";
            ProdWs = "wss://not.exist:9443";

            TestHttps = "https://test.not.exist";
            TestWs = "wss://test.not.exist";
        }
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
