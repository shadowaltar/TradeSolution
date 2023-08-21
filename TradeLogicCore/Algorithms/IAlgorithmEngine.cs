using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public abstract class IAlgorithmEngine<T> where T : IAlgorithmVariables
{
    public IAlgorithm<T> Algorithm { get; }

    public IPositionSizingAlgoLogic<T> Sizing { get; protected set; }

    public IEnterPositionAlgoLogic<T> EnterLogic { get; protected set; }

    public IExitPositionAlgoLogic<T> ExitLogic { get; protected set; }

    public ISecurityScreeningAlgoLogic Screening { get; protected set; }

    protected abstract void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice);
}
