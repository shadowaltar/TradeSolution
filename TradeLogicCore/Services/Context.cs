using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public class Context
{
    public EnvironmentType Environment { get; private set; }
    public ExchangeType Exchange { get; private set; }
    public BrokerType Broker { get; private set; }
    public int BrokerId { get; private set; }

    public void Setup(EnvironmentType environment, ExchangeType exchange, BrokerType broker, int brokerId)
    {
        Environment = environment;
        Exchange = exchange;
        Broker = broker;
        BrokerId = brokerId;

        ExternalQueryStates.Exchange = exchange;
        ExternalQueryStates.Environment = environment;
        ExternalQueryStates.Broker = broker;
    }
}
