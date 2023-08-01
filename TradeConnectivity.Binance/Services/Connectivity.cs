using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Services;

public class Connectivity : IExternalConnectivityManagement
{
    public void SetEnvironment(EnvironmentType environmentType)
    {
        RootUrls.SetEnvironment(environmentType);
    }
}
