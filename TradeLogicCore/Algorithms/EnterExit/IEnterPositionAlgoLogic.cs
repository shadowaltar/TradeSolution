using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IPositionSizingAlgoLogic<T> Sizing { get; }
   
    IUpfrontFeeLogic<T>? UpfrontFeeLogic { get; set; }

    void Open(IAlgorithemContext<T> context, AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice);
}
