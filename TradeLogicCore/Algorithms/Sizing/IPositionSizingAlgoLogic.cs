using TradeCommon.Essentials.Portfolios;

namespace TradeLogicCore.Algorithms.Sizing;
public interface IPositionSizingAlgoLogic<T> where T : IAlgorithmVariables
{
    abstract decimal GetSize(decimal availableCash, AlgoEntry<T> current, AlgoEntry<T> last, decimal price, DateTime time);
}
