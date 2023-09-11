using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public class SimplePositionSizing<T> : IPositionSizingAlgoLogic<T> where T : IAlgorithmVariables
{
    public SimplePositionSizing(IAlgorithm<T> algorithm)
    {
        Algorithm = algorithm;
    }

    public IAlgorithm<T> Algorithm { get; }

    public decimal GetSize(decimal availableCash, AlgoEntry<T> current, AlgoEntry<T> last, decimal price, DateTime time)
    {
        // a simple one which invest all and without lot-size rounding
        var setAside = 0m;
        var quantity = (availableCash - setAside) / price;
        return quantity;
    }
}