using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public class Context
{
    //private readonly IExternalConnectivityManagement _external;
    public EnvironmentType EnvironmentType { get; private set; }
    public ExchangeType ExchangeType { get; private set; }
    public BrokerType BrokerType { get; private set; }
    public int BrokerId { get; private set; }

    public void Setup(EnvironmentType environmentType, ExchangeType exchangeType, BrokerType brokerType, int brokerId)
    {
        EnvironmentType = environmentType;
        ExchangeType = exchangeType;
        BrokerType = brokerType;
        BrokerId = brokerId;
    }



    //public Context(ExchangeType exchangeType,
    //               BrokerType brokerType)
    //{
    //    ExchangeType = exchangeType;
    //    BrokerType = brokerType;
    //    BrokerId = ExternalNames.BrokerTypeToIds[BrokerType];
    //}

    //public void SetEnvironment(EnvironmentType type)
    //{
    //    _external.SetEnvironment(type);
    //    EnvironmentType = type;
    //}
}
