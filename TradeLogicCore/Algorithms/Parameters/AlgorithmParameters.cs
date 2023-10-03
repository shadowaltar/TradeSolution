using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeLogicCore.Algorithms.Parameters;

public record AlgorithmParameters(bool IsBackTesting,
                                  string UserName,
                                  string AccountName,
                                  EnvironmentType Environment,
                                  ExchangeType Exchange,
                                  BrokerType Broker,
                                  IntervalType Interval,
                                  List<Security> SecurityPool,
                                  AlgoEffectiveTimeRange TimeRange)
{
    public bool ShouldCloseOpenPositionsWhenHalted { get; set; } = true;
}

public record EngineParameters(bool CloseOpenOrdersOnStart = true,
                               bool CloseOpenPositionsOnStop = true,
                               bool CloseOpenPositionsOnStart = true,
                               bool CloseNonFiatAssetPositionsOnStart = false);


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