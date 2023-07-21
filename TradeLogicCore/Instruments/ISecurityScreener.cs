using TradeCommon.Essentials.Instruments;
using TradeCommon.Utils.Evaluation;

namespace TradeLogicCore.Instruments;

public interface ISecurityScreener
{
    List<Security> Filter(List<Security> securities,
                          DateTime asOfTime,
                          List<Criteria> indicators,
                          BoolOp indicatorConjunction = BoolOp.And);
}