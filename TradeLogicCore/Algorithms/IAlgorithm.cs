using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm
{
    int Id { get; }
    int VersionId { get; }
    IEnterPositionAlgoLogic Entering { get; }
    IExitPositionAlgoLogic Exiting { get; }
    ISecurityScreeningAlgoLogic Screening { get; }
    IPositionSizingAlgoLogic Sizing { get; }
    decimal LongStopLossRatio { get; }
    decimal LongTakeProfitRatio { get; }
    decimal ShortStopLossRatio { get; }
    decimal ShortTakeProfitRatio { get; }
}

public interface IAlgorithm<T> : IAlgorithm where T : IAlgorithmVariables
{
    T CalculateVariables(decimal price, AlgoEntry<T>? last);

    bool IsOpenLongSignal(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseLongSignal(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsShortSignal(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    bool IsCloseShortSignal(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return false; }

    void BeforeSignalDetection(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    void AfterSignalDetection(AlgoEntry<T> current, AlgoEntry<T>? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    void BeforeAlgoExecution() { }
    void AfterAlgoExecution() { }
    void BeforeProcessingSecurity(Security security) { }
    void AfterProcessingSecurity(Security security) { }
    void BeforeOpeningLong(AlgoEntry entry) { }
    void AfterLongOpened(AlgoEntry entry) { }
    void BeforeClosingLong(AlgoEntry entry) { }
    void AfterLongClosed(AlgoEntry entry) { }
    void BeforeStopLossLong(AlgoEntry entry) { }
    void AfterStopLossLong(AlgoEntry entry) { }


    void BeforeOpeningShort(AlgoEntry entry) { }
    void AfterShortOpened(AlgoEntry entry) { }
    void BeforeClosingShort(AlgoEntry entry) { }
    void AfterShortClosed(AlgoEntry entry) { }
    void BeforeStopLossShort(AlgoEntry entry) { }
    void AfterStopLossShort(AlgoEntry entry) { }
}