using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.CryptoSimulator.Services;

public class Connectivity : IExternalConnectivityManagement
{
    public string RootUrl { get; protected set; }

    public string RootWebSocketUrl { get; protected set; }

    public Connectivity()
    {
        SetEnvironment(EnvironmentType.Simulation);
    }

    public void SetEnvironment(EnvironmentType environment)
    {
        switch (environment)
        {
            case EnvironmentType.Simulation:
                break;
            default: throw new ArgumentException("Invalid environment type", nameof(environment));
        }
    }

    public bool Ping(out string url)
    {
        url = "localhost/ping";
        return true;
    }

    public double GetAverageLatency()
    {
        return 0;
    }
}
