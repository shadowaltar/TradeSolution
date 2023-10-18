using TradeCommon.Algorithms;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm
{
    int Id { get; }
    int VersionId { get; }
    bool IsShortSellAllowed { get; }
    decimal LongStopLossRatio { get; }
    decimal LongTakeProfitRatio { get; }
    decimal ShortStopLossRatio { get; }
    decimal ShortTakeProfitRatio { get; }
    AlgorithmParameters AlgorithmParameters { get; }

    IEnterPositionAlgoLogic Entering { get; }
    IExitPositionAlgoLogic Exiting { get; }
    ISecurityScreeningAlgoLogic Screening { get; }
    IPositionSizingAlgoLogic Sizing { get; }

    IAlgorithmVariables CalculateVariables(decimal price, AlgoEntry? last);

    void NotifyPositionChanged(Position position) { }

    void BeforeSignalDetection(AlgoEntry current, AlgoEntry? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    void AfterSignalDetection(AlgoEntry current, AlgoEntry? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

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
    List<Order> PickOpenOrdersToCleanUp(AlgoEntry current) { return new(); }
    void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice lastPrice);
    bool CanOpenLong(AlgoEntry current);
    bool CanOpenShort(AlgoEntry current);
    bool CanCloseLong(AlgoEntry current);
    bool CanCloseShort(AlgoEntry current);
    bool CanCancel(AlgoEntry current);
}