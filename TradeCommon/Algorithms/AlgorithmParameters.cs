using Common.Attributes;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Algorithms;

public record AlgorithmParameters(bool IsBackTesting,
                                  IntervalType Interval,
                                  [DatabaseIgnore]
                                  List<Security> SecurityPool,
                                  List<string> SecurityCodes,
                                  AlgoEffectiveTimeRange TimeRange,
                                  bool RequiresTickData,
                                  StopOrderStyleType StopOrderTriggerBy = StopOrderStyleType.RealOrder,
                                  decimal LongStopLossRatio = 0,
                                  decimal LongTakeProfitRatio = 0,
                                  decimal ShortStopLossRatio = 0,
                                  decimal ShortTakeProfitRatio = 0,
                                  IAlgorithmVariables? OtherVariables = null,
                                  BidAsk TickPriceTriggerForSell = BidAsk.Bid,
                                  BidAsk TickPriceTriggerForBuy = BidAsk.Ask);