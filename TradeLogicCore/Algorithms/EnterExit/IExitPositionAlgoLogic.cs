using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.FeeCalculation;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic
{

    decimal LongStopLossRatio { get; }

    decimal LongTakeProfitRatio { get; }

    decimal ShortStopLossRatio { get; }

    decimal ShortTakeProfitRatio { get; }

    ITransactionFeeLogic? FeeLogic { get; set; }

    Task<ExternalQueryState> Close(AlgoEntry? current, Security security, decimal triggerPrice, Side exitSide, DateTime exitTime, OrderActionType actionType);

    void BackTestClose(AlgoEntry current, decimal exitPrice, DateTime exitTime);

    void BackTestStopLoss(AlgoEntry current, AlgoEntry last, DateTime exitTime);

    void OnCloseOrderAcknowledged();

    void OnTakeProfitTriggered();

    void OnStopLossTriggered();
}
