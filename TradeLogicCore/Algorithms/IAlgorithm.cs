using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm<T> where T : IAlgorithmVariables
{
    IAlgorithmContext<T> Context { get; }
    IServices Services { get; }
    IPositionSizingAlgoLogic<T> Sizing { get; }
    IEnterPositionAlgoLogic<T> Entering { get; }
    IExitPositionAlgoLogic<T> Exiting { get; }
    ISecurityScreeningAlgoLogic Screening { get; }

    T CalculateVariables(decimal price, AlgoEntry<T>? last);

    bool IsOpenLongSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseLongSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsShortSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseShortSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    void BeforeSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }
    void AfterSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    void BeforeAlgoExecution(IAlgorithmContext<T> context) { }
    void AfterAlgoExecution(IAlgorithmContext<T> context) { }
    void BeforeProcessingSecurity(IAlgorithmContext<T> context, Security security) { }
    void AfterProcessingSecurity(IAlgorithmContext<T> context, Security security) { }
    void BeforeOpeningLong(AlgoEntry<T> entry) { }
    void AfterLongOpened(AlgoEntry<T> entry) { }
    void BeforeClosingLong(AlgoEntry<T> entry) { }
    void AfterLongClosed(AlgoEntry<T> entry) { }
    void BeforeStopLossLong(AlgoEntry<T> entry) { }
    void AfterStopLossLong(AlgoEntry<T> entry) { }


    void BeforeOpeningShort(AlgoEntry<T> entry) { }
    void AfterShortOpened(AlgoEntry<T> entry) { }
    void BeforeClosingShort(AlgoEntry<T> entry) { }
    void AfterShortClosed(AlgoEntry<T> entry) { }
    void BeforeStopLossShort(AlgoEntry<T> entry) { }
    void AfterStopLossShort(AlgoEntry<T> entry) { }
}