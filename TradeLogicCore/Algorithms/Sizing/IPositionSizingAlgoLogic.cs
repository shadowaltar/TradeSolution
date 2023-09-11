namespace TradeLogicCore.Algorithms.Sizing;
public interface IPositionSizingAlgoLogic<T> where T : IAlgorithmVariables
{
    IAlgorithm<T> Algorithm { get; }

    abstract decimal GetSize(decimal availableCash, AlgoEntry<T> current, AlgoEntry<T> last, decimal price, DateTime time);
}