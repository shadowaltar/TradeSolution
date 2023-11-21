using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeDesk.Utils;
public record ClientSession(string UserName,
                            string AccountName,
                            EnvironmentType Environment,
                            ExchangeType Exchange,
                            BrokerType Broker,
                            string SessionToken)
{
}
