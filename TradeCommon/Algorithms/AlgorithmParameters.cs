using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Algorithms;

public record AlgorithmParameters(bool IsBackTesting,
                                  IntervalType Interval,
                                  List<Security> SecurityPool,
                                  AlgoEffectiveTimeRange TimeRange,
                                  OriginType StopOrderTriggerBy = OriginType.AlgorithmLogic,
                                  BidAsk TickPriceTriggerForSell = BidAsk.Bid,
                                  BidAsk TickPriceTriggerForBuy = BidAsk.Ask);