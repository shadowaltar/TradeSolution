using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;
public interface ISecurityScreeningAlgoLogic
{
    void SetAndPick(IDictionary<long, Security> securityPool);

    IReadOnlyDictionary<long, Security> GetPickedOnes();

    IReadOnlyDictionary<long, Security> GetAll();

    bool CheckIsPicked(long securityId);

    void Repick();

    /// <summary>
    /// Check if the picked ones are changed.
    /// If yes, returns true, and internally set it to false (has side effect);
    /// it becomes true again when the picked ones are changed again later.
    /// </summary>
    /// <returns></returns>
    bool TryCheckIfChanged(out IReadOnlyDictionary<long, Security> pickedOnes);
}
