using TradeCommon.Constants;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public class Context
{
    private readonly IExternalConnectivityManagement _external;
    public EnvironmentType EnvironmentType { get; private set; } = EnvironmentType.Unknown;
    public ExchangeType ExchangeType { get; }
    public BrokerType BrokerType { get; }
    public int BrokerId { get; }

    public Context(ExchangeType exchangeType,
                   BrokerType brokerType)
    {
        ExchangeType = exchangeType;
        BrokerType = brokerType;
        BrokerId = ExternalNames.BrokerTypeToIds[BrokerType];
    }

    public void SetEnvironment(EnvironmentType type)
    {
        _external.SetEnvironment(type);
        EnvironmentType = type;
    }
}
