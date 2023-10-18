using TradeCommon.Algorithms;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmEngine
{
    event Action ReachedDesignatedEndTime;

    IPositionSizingAlgoLogic? Sizing { get; }

    IEnterPositionAlgoLogic? EnterLogic { get; }

    IExitPositionAlgoLogic? ExitLogic { get; }

    ISecurityScreeningAlgoLogic? Screening { get; }

    User? User { get; }

    Account? Account { get; }

    /// <summary>
    /// Total signal count being processed. It is usually the count of prices / ticks.
    /// </summary>
    int TotalPriceEventCount { get; }

    DateTime? DesignatedHaltTime { get; }

    DateTime? DesignatedResumeTime { get; }

    DateTime? DesignatedStartTime { get; }

    DateTime? DesignatedStopTime { get; }

    int? HoursBeforeHalt { get; }

    IntervalType Interval { get; }

    AlgoStopTimeType WhenToStopOrHalt { get; }

    AlgorithmParameters? AlgoParameters { get; }

    EngineParameters? EngineParameters { get; }

    Task<int> Run(AlgorithmParameters parameters);

    Task Stop();

    AlgoBatch AlgoBatch { get; }

    IAlgorithm? Algorithm { get; }

    /// <summary>
    /// Gets all the algo entries created during engine execution.
    /// </summary>
    List<AlgoEntry> GetAllEntries(int securityId);

    /// <summary>
    /// Gets the algo entries only related to trading activities during engine execution.
    /// </summary>
    List<AlgoEntry> GetExecutionEntries(int securityId);

    /// <summary>
    /// Runs the procedure to update the engine internal status.
    /// Usually triggered by new price or new market events.
    /// </summary>
    Task Update(int securityId, OhlcPrice ohlcPrice);
}