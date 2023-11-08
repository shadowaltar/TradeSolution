using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Algorithms;

public record AlgorithmParameters(bool IsBackTesting,
                                  IntervalType Interval,
                                  List<Security> SecurityPool,
                                  AlgoEffectiveTimeRange TimeRange,
                                  bool RequiresTickData = false,
                                  OriginType StopOrderTriggerBy = OriginType.UpfrontOrder,
                                  BidAsk TickPriceTriggerForSell = BidAsk.Bid,
                                  BidAsk TickPriceTriggerForBuy = BidAsk.Ask);