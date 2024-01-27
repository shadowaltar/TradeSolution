using TradeCommon.Runtime;

namespace TradeCommon.Externals;

public interface IExternalConnectivityManagement
{
    string RootUrl { get; }

    string RootWebSocketUrl { get; }

    bool Ping(out string url);

    double GetAverageLatency();

    void SetEnvironment(EnvironmentType environmentType);
}
