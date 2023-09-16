using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic
{
    IPositionSizingAlgoLogic Sizing { get; }

    ITransactionFeeLogic? FeeLogic { get; set; }

    List<Order> Open(AlgoEntry current,
                     AlgoEntry? last,
                     decimal enterPrice,
                     Side side,
                     DateTime enterTime,
                     decimal stopLossPrice,
                     decimal takeProfitPrice);

    void BackTestOpen(AlgoEntry current,
                      AlgoEntry? last,
                      decimal enterPrice,
                      Side side,
                      DateTime enterTime,
                      decimal stopLossPrice,
                      decimal takeProfitPrice);
}
