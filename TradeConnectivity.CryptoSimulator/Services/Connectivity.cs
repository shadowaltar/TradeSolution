using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.CryptoSimulator.Services;

public class Connectivity : IExternalConnectivityManagement
{
    private const string ProdHttps = "https://localhost:7065";
    private const string ProdWs = "wss://localhost:7065";

    private const string TestHttps = "https://localhost:7065";
    private const string TestWs = "wss://localhost:7065";

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

    public bool Ping()
    {
        return true;
    }

    public double GetAverageLatency()
    {
        return 0;
    }
}
