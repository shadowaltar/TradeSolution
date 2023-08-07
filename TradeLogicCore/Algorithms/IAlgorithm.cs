using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm<T> where T : IAlgorithmVariables
{
    IAlgorithemContext<T> Context { get; set; }

    T CalculateVariables(decimal price, AlgoEntry<T>? last);

    bool IsLongSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice ohlcPrice);

    bool IsShortSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice ohlcPrice);
}