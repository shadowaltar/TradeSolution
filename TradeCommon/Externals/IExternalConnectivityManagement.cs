using TradeCommon.Runtime;

namespace TradeCommon.Externals;

public interface IExternalConnectivityManagement
{
    void SetEnvironment(EnvironmentType environmentType);

    string RootUrl { get; }

    string RootWebSocketUrl { get; }
}
