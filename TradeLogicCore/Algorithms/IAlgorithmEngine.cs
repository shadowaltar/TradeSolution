using TradeCommon.Essentials.Accounts;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public abstract class IAlgorithmEngine<T> where T : IAlgorithmVariables
{
    public abstract Task Run(AlgoStartupParameters parameters);

    public abstract Task Stop();

    public abstract IAlgorithm<T> Algorithm { get; }

    public abstract User? User { get; protected set; }

    public abstract Account? Account { get; protected set; }

    public abstract decimal InitialFreeAmount { get; protected set; }

    public abstract IPositionSizingAlgoLogic<T> Sizing { get; protected set; }

    public abstract IEnterPositionAlgoLogic<T> EnterLogic { get; protected set; }

    public abstract IExitPositionAlgoLogic<T> ExitLogic { get; protected set; }

    public abstract ISecurityScreeningAlgoLogic Screening { get; protected set; }

    protected abstract void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice);
}
