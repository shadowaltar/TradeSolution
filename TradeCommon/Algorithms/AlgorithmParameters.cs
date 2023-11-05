using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeCommon.Algorithms;

public record AlgorithmParameters(bool IsBackTesting,
                                  IntervalType Interval,
                                  List<Security> SecurityPool,
                                  AlgoEffectiveTimeRange TimeRange,
                                  BidAsk TickPriceTriggerForSell = BidAsk.Bid,
                                  BidAsk TickPriceTriggerForBuy = BidAsk.Ask);