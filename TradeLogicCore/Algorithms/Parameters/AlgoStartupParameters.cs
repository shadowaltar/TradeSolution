using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeLogicCore.Algorithms.Parameters;

public record AlgoStartupParameters(string UserName,
                                         string Password,
                                         string AccountName,
                                         EnvironmentType Environment,
                                         ExchangeType Exchange,
                                         BrokerType Broker,
                                         IntervalType Interval,
                                         List<Security> BasicSecurityPool,
                                         AlgoEffectiveTimeRange EffectiveTimeRange);