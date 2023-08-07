using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;
public interface ISecurityScreeningAlgoLogic<T>
{
    IReadOnlyList<Security> Pick(List<Security> securityPool);
}
