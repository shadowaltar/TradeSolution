using Common.Attributes;
using TradeCommon.Algorithms;
using TradeCommon.Database;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Algorithms;

[Storage("algo_batches", DatabaseNames.AlgorithmData, SortProperties = false)]
[Unique(nameof(Id))]
[Index(nameof(AlgoId))]
[Index(nameof(StartTime))]
public record AlgoBatch()
{
    /// <summary>
    /// Unique Id which is different for every algo execution. It should be in UUID format.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Id of algorithm.
    /// </summary>
    public int AlgoId { get; set; }

    /// <summary>
    /// Name of algorithm.
    /// </summary>
    public string? AlgoName { get; set; }

    /// <summary>
    /// Version Id from the algorithm.
    /// </summary>
    public int AlgoVersionId { get; set; }

    /// <summary>
    /// The user's Id which executes the algorithm.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The account's Id which executes the algorithm.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// The environment which executes the algorithm.
    /// </summary>
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Algorithm execution start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Algorithm execution end time.
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// Algorithm execution's engine parameters.
    /// </summary>
    [AsJson]
    public EngineParameters? EngineParameters { get; set; }

    /// <summary>
    /// Algorithm's parameters.
    /// </summary>
    [AsJson]
    public AlgorithmParameters? AlgorithmParameters { get; set; }
}
