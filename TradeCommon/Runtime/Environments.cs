using TradeCommon.Externals;

namespace TradeCommon.Runtime;
public class Environments
{
    private readonly IExternalConnectivityManagement _external;

    public Environments(IExternalConnectivityManagement external)
    {
        _external = external;
    }

    public void SetEnvironment(EnvironmentType type)
    {
        _external.SetEnvironment(type);
    }
}

