using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IAlgorithm<T> Algorithm { get; }

    IPositionSizingAlgoLogic<T> Sizing { get; }
   
    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    void Open(AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice);
}
