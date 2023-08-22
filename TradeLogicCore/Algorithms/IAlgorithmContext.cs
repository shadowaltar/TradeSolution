using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmContext<T>
{
    Portfolio Portfolio { get; }

    List<Security> SecurityPool { get; }
    
    /// <summary>
    /// Gets the list of entries representing currently opened positions.
    /// Key is the entry's id.
    /// </summary>
    Dictionary<long, AlgoEntry<T>> OpenedEntries { get; }
}