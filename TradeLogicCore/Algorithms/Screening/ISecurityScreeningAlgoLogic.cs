using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;
public interface ISecurityScreeningAlgoLogic
{
    void SetAndPick(List<Security> securityPool);

    IReadOnlyCollection<Security> GetPickedOnes();

    bool CheckIsPicked(int securityId);
    
    void Repick();
}
