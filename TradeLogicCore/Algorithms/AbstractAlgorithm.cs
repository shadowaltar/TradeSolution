using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using static TradeLogicCore.Algorithms.Rumi;

namespace TradeLogicCore.Algorithms;
public abstract class AbstractAlgorithm<T> where T : IAlgorithmVariables
{
    protected RuntimePosition<T>? FirstPosition { get; set; }

    private bool IsLoggingEnabled { get; set; }

    private IPositionSizingAlgoLogic Sizing { get; }
    private IEnterPositionAlgoLogic Entering { get; }
    private IExitPositionAlgoLogic Exiting { get; }
    private ISecuritySelectionAlgoLogic Screening { get; }

    protected abstract T CalculateVariables(decimal price, RuntimePosition<T>? last);
    protected abstract bool IsLongSignal(RuntimePosition<T> current, RuntimePosition<T> last, OhlcPrice ohlcPrice);
    protected abstract bool IsShortSignal(RuntimePosition<T> current, RuntimePosition<T> last, OhlcPrice ohlcPrice);
    protected abstract void OpenPosition(RuntimePosition<T> current, RuntimePosition<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice);
    protected abstract void ClosePosition(RuntimePosition<T> current, decimal exitPrice, DateTime exitTime);
    protected abstract void StopLoss(RuntimePosition<T> current, RuntimePosition<T> last, DateTime exitTime);
    protected abstract void CopyPosition(RuntimePosition<T> current, RuntimePosition<T> last, decimal currentPrice);
}
