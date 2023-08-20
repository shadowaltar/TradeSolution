using TradeLogicCore.Algorithms.FeeCalculation;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    decimal StopLossRatio { get; }

    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    void Close(IAlgorithmContext<T> context, AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void StopLoss(IAlgorithmContext<T> context, AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime);
}
