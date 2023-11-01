using Common;
using System.Diagnostics;
using System.Security.Cryptography;
using TradeCommon.Algorithms;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public abstract class Algorithm
{
    protected WorkingItemMonitor<Position> _closingPositionMonitor;

    public virtual int Id { get; }
    public virtual int VersionId { get; }
    public virtual bool IsShortSellAllowed { get; }
    public virtual decimal LongStopLossRatio { get; }
    public virtual decimal LongTakeProfitRatio { get; }
    public virtual decimal ShortStopLossRatio { get; }
    public virtual decimal ShortTakeProfitRatio { get; }
    public virtual AlgorithmParameters AlgorithmParameters { get; }

    public virtual IEnterPositionAlgoLogic Entering { get; set; }
    public virtual IExitPositionAlgoLogic Exiting { get; set; }
    public virtual ISecurityScreeningAlgoLogic Screening { get; set; }
    public virtual IPositionSizingAlgoLogic Sizing { get; set; }

    public abstract IAlgorithmVariables CalculateVariables(decimal price, AlgoEntry? last);

    public virtual void BeforeSignalDetection(AlgoEntry current, AlgoEntry? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    public virtual void AfterSignalDetection(AlgoEntry current, AlgoEntry? last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    public virtual void BeforeAlgoExecution() { }
    public virtual void AfterAlgoExecution() { }
    public virtual void BeforeProcessingSecurity(Security security) { }
    public virtual void AfterProcessingSecurity(Security security) { }
    public abstract void AfterPositionCreated(AlgoEntry current);
    public abstract void AfterPositionUpdated(AlgoEntry current);
    public abstract void AfterPositionClosed(AlgoEntry entry);
    public abstract void AfterStoppedLoss(AlgoEntry entry, Side stopLossSide);
    public abstract void AfterTookProfit(AlgoEntry entry, Side takeProfitSide);

    public virtual List<Order> PickOpenOrdersToCleanUp(AlgoEntry current) { return new(); }
    public abstract void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice lastPrice);
    public abstract bool CanOpenLong(AlgoEntry current);
    public abstract bool CanOpenShort(AlgoEntry current);
    public abstract bool CanCloseLong(AlgoEntry current);
    public abstract bool CanCloseShort(AlgoEntry current);
    public abstract bool ShallStopLoss(int securityId, Tick tick);
    public abstract bool ShallTakeProfit(int securityId, Tick tick);
    public abstract bool CanCancel(AlgoEntry current);

    protected Algorithm()
    {
        _closingPositionMonitor = new WorkingItemMonitor<Position>();
    }

    public virtual decimal GetStopLossPrice(decimal price, Side parentOrderSide, Security security)
    {
        decimal slRatio = parentOrderSide switch
        {
            Side.Buy => LongStopLossRatio,
            Side.Sell => -ShortStopLossRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        if (!slRatio.IsValid() && slRatio <= 0)
            return 0;
        return security.GetStopLossPrice(price, slRatio);
    }

    public virtual decimal GetTakeProfitPrice(decimal price, Side parentOrderSide, Security security)
    {
        decimal tpRatio = parentOrderSide switch
        {
            Side.Buy => LongTakeProfitRatio,
            Side.Sell => -ShortTakeProfitRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        if (!tpRatio.IsValid() && tpRatio <= 0)
            return 0;
        return security.GetTakeProfitPrice(price, tpRatio);
    }

    public virtual async Task<ExternalQueryState> Open(AlgoEntry current, AlgoEntry last, decimal price, Side enterSide, DateTime time)
    {
        var sl = GetStopLossPrice(price, enterSide, current.Security);
        var tp = GetTakeProfitPrice(price, enterSide, current.Security);
        return await Entering.Open(current, last, price, enterSide, time, sl, tp);
    }

    public abstract Task<ExternalQueryState> Close(AlgoEntry current, Security security, Side exitSide, DateTime exitTime, OrderActionType actionType);

    public abstract Task<ExternalQueryState> CloseByTickStopLoss(Position position);

    public abstract Task<ExternalQueryState> CloseByTickTakeProfit(Position position);
}