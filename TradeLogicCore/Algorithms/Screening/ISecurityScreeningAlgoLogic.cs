using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;
public interface ISecurityScreeningAlgoLogic
{
    IReadOnlyCollection<Security> GetPickedOnes(List<Security> securityPool);

    void Pick(List<Security> securityPool);

    bool CheckIsPicked(int securityId);
}
