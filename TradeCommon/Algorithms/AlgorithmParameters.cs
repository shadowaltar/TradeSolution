using Common.Attributes;
using System.Text;
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
                                  BidAsk TickPriceTriggerForBuy = BidAsk.Ask)
{

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("\"Interval\":\"").Append(Interval).AppendLine("\",");
        sb.Append("\"SecurityCodes\":\"").AppendJoin(",", SecurityCodes).AppendLine("\",");
        sb.Append("\"RequiresTickData\":").Append(RequiresTickData).AppendLine(",");
        sb.Append("\"StopOrderTriggerBy\":\"").Append(StopOrderTriggerBy.ToString()).AppendLine("\",");
        sb.Append("\"LongStopLossRatio\":").Append(LongStopLossRatio).AppendLine(",");
        sb.Append("\"LongTakeProfitRatio\":").Append(LongTakeProfitRatio).AppendLine(",");
        sb.Append("\"ShortStopLossRatio\":").Append(ShortStopLossRatio).AppendLine(",");
        sb.Append("\"ShortTakeProfitRatio\":").Append(ShortTakeProfitRatio).AppendLine(",");
        sb.Append(OtherVariables?.ToString());
       
        return sb.ToString();
    }

}