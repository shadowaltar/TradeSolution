using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm<T> where T : IAlgorithmVariables
{
    IAlgorithemContext<T> Context { get; set; }

    T CalculateVariables(decimal price, AlgoEntry<T>? last);

    bool IsOpenLongSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseLongSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsShortSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseShortSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    void BeforeSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }
    void AfterSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

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