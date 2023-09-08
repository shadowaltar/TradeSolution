using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmEngine
{
    Task<int> Run(AlgoStartupParameters parameters);
    Task Stop();
}

public interface IAlgorithmEngine<T> : IAlgorithmEngine where T : IAlgorithmVariables
{
    event Action ReachedDesignatedEndTime;

    IAlgorithm<T> Algorithm { get; }

    User? User { get; }

    Account? Account { get; }

    IPositionSizingAlgoLogic<T> Sizing { get; }

    IEnterPositionAlgoLogic<T> EnterLogic { get; }

    IExitPositionAlgoLogic<T> ExitLogic { get; }

    ISecurityScreeningAlgoLogic Screening { get; }

    /// <summary>
    /// Total signal count being processed. It is usually the count of prices / ticks.
    /// </summary>
    int TotalSignalCount { get; }

    DateTime? DesignatedHaltTime { get; }

    DateTime? DesignatedResumeTime { get; }

    DateTime? DesignatedStartTime { get; }

    DateTime? DesignatedStopTime { get; }

    int? HoursBeforeHalt { get; }

    IntervalType Interval { get; }

    bool ShouldCloseOpenPositionsWhenHalted { get; }

    bool ShouldCloseOpenPositionsWhenStopped { get; }

    AlgoStopTimeType WhenToStopOrHalt { get; }

    /// <summary>
    /// Gets all the algo entries created during engine execution.
    /// </summary>
    List<AlgoEntry<T>> GetAllEntries(int securityId);

    /// <summary>
    /// Gets the algo entries only related to trading activities during engine execution.
    /// </summary>
    List<AlgoEntry<T>> GetExecutionEntries(int securityId);

    /// <summary>
    /// Gets the algo entries currently stands for opened positions.
    /// </summary>
    Dictionary<long, AlgoEntry<T>> GetOpenEntries(int securityId);
}
