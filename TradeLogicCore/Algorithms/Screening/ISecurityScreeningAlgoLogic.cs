using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;
public interface ISecurityScreeningAlgoLogic<T> where T : IAlgorithmVariables
{
    void SetAndPick(IDictionary<int, Security> securityPool);

    IReadOnlyDictionary<int, Security> GetPickedOnes();

    IReadOnlyDictionary<int, Security> GetAll();

    bool CheckIsPicked(int securityId);

    void Repick();

    /// <summary>
    /// Check if the picked ones are changed.
    /// If yes, returns true, and internally set it to false (has side effect);
    /// it becomes true again when the picked ones are changed again later.
    /// </summary>
    /// <returns></returns>
    bool TryCheckIfChanged(out IReadOnlyDictionary<int, Security> pickedOnes);
}
