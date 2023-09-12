using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;
public interface IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    IAlgorithm<T> Algorithm { get; }

    IPositionSizingAlgoLogic<T> Sizing { get; }

    ITransactionFeeLogic<T>? FeeLogic { get; set; }

    Order Open(AlgoEntry<T> current,
               AlgoEntry<T> last,
               decimal enterPrice,
               Side side,
               DateTime enterTime,
               decimal stopLossPrice,
               decimal takeProfitPrice);

    void BackTestOpen(AlgoEntry<T> current,
                      AlgoEntry<T> last,
                      decimal enterPrice,
                      Side side,
                      DateTime enterTime,
                      decimal stopLossPrice,
                      decimal takeProfitPrice);
}
