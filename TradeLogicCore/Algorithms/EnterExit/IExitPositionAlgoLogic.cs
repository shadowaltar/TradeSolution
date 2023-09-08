using TradeLogicCore.Algorithms.FeeCalculation;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IAlgorithm<T> MainAlgo { get; }

    decimal StopLossRatio { get; }

    decimal TakeProfitRatio { get; }

    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    void Close(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void StopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime);

    void OnCloseOrderAcknowledged();

    void OnTakeProfitTriggered();

    void OnStopLossTriggered();
}
