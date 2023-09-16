using TradeCommon.Essentials.Algorithms;
using TradeLogicCore.Algorithms.FeeCalculation;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic
{
    decimal LongStopLossRatio { get; }

    decimal LongTakeProfitRatio { get; }

    decimal ShortStopLossRatio { get; }

    decimal ShortTakeProfitRatio { get; }

    ITransactionFeeLogic? FeeLogic { get; set; }

    void Close(AlgoEntry current, decimal exitPrice, DateTime exitTime);

    void BackTestClose(AlgoEntry current, decimal exitPrice, DateTime exitTime);

    void BackTestStopLoss(AlgoEntry current, AlgoEntry last, DateTime exitTime);

    void OnCloseOrderAcknowledged();

    void OnTakeProfitTriggered();

    void OnStopLossTriggered();
}
