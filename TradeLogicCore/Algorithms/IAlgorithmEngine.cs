using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Portfolios;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public interface IAlgorithmEngine<T> where T : IAlgorithmVariables
{
    event Action ReachedDesignatedEndTime;

    Task Run(AlgoStartupParameters parameters);

    Task Stop();

    IAlgorithm<T> Algorithm { get; }

    User? User { get; }

    Account? Account { get; }

    decimal InitialFreeAmount { get; }

    IPositionSizingAlgoLogic<T> Sizing { get; }

    IEnterPositionAlgoLogic<T> EnterLogic { get; }

    IExitPositionAlgoLogic<T> ExitLogic { get; }

    ISecurityScreeningAlgoLogic Screening { get; }

    DateTime? DesignatedHaltTime { get; }

    DateTime? DesignatedResumeTime { get; }

    DateTime? DesignatedStartTime { get; }

    DateTime? DesignatedStopTime { get; }

    int? HoursBeforeHalt { get; }

    IntervalType Interval { get; }

    Dictionary<long, AlgoEntry<T>> OpenedEntries { get; }

    List<Position> OpenPositions { get; }

    Portfolio Portfolio { get; }

    bool ShouldCloseOpenPositionsWhenHalted { get; }

    bool ShouldCloseOpenPositionsWhenStopped { get; }

    AlgoStopTimeType WhenToStopOrHalt { get; }
}
