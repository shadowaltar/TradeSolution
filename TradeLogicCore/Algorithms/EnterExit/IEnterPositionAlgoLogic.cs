using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IPositionSizingAlgoLogic<T> Sizing { get; }
   
    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    void Open(IAlgorithmContext<T> context, AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice);
}
