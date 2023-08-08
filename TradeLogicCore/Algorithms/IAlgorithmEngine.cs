using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public abstract class IAlgorithmEngine<T> where T : IAlgorithmVariables
{
    public IAlgorithm<T> Algorithm { get; }

    public IPositionSizingAlgoLogic<T> Sizing { get; }

    public IEnterPositionAlgoLogic<T> EnterLogic { get; }

    public IExitPositionAlgoLogic<T> ExitLogic { get; }

    public ISecurityScreeningAlgoLogic<T> Screening { get; }

    protected abstract void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice);
}
