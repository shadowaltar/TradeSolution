using TradeLogicCore.Algorithms.FeeCalculation;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IAlgorithm<T> Algorithm { get; }

    decimal LongStopLossRatio { get; }

    decimal LongTakeProfitRatio { get; }

    decimal ShortStopLossRatio { get; }

    decimal ShortTakeProfitRatio { get; }

    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    void Close(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void BackTestClose(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void BackTestStopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime);

    void OnCloseOrderAcknowledged();

    void OnTakeProfitTriggered();

    void OnStopLossTriggered();
}
