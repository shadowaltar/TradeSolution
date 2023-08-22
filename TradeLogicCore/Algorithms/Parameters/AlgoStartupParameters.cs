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
                                    List<Security> SecurityPool,
                                    AlgoEffectiveTimeRange TimeRange)
{
    public bool ShouldCloseOpenPositionsWhenStopped { get; set; } = true;
    public bool ShouldCloseOpenPositionsWhenHalted { get; set; } = true;
}


public enum AlgoStartTimeType
{
    Never,
    Immediately,
    Designated,
    NextStartOf,
    NextStartOfLocalDay,
    NextMarketOpens,
    NextWeekMarketOpens,
}


public enum AlgoStopTimeType
{
    Never,
    Designated,
    BeforeBrokerMaintenance,
}