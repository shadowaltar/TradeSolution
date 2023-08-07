using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IPositionSizingAlgoLogic<T> Sizing { get; }

    void Open(AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice);
}
