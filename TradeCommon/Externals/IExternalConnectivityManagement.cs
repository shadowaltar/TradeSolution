using TradeCommon.Runtime;

namespace TradeCommon.Externals;

public interface IExternalConnectivityManagement
{
    void SetEnvironment(EnvironmentType environmentType);
}
