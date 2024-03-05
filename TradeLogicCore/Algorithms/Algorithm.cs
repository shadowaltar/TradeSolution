﻿using Common;
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
    protected WorkingItemMonitor<Asset> _closingPositionMonitor;

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
    public abstract void AfterPositionChanged(AlgoEntry current);
    public abstract void AfterStoppedLoss(AlgoEntry entry);
    public abstract void AfterTookProfit(AlgoEntry entry);

    public virtual List<Order> PickOpenOrdersToCleanUp(AlgoEntry current) { return []; }
    public abstract void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice? lastPrice);
    public abstract bool CanOpen(AlgoEntry current, Side side);
    public abstract bool CanClose(AlgoEntry current, Side side);
    public abstract bool ShallStopLoss(AlgoEntry current, Tick tick, out decimal triggerPrice);
    public abstract bool ShallTakeProfit(AlgoEntry current, Tick tick, out decimal triggerPrice);
    public abstract bool CanCancel(AlgoEntry current);

    protected Algorithm()
    {
        _closingPositionMonitor = new WorkingItemMonitor<Asset>();
    }

    public virtual decimal GetStopLossPrice(decimal price, Side parentOrderSide, Security security)
    {
        decimal slRatio = parentOrderSide switch
        {
            Side.Buy => LongStopLossRatio,
            Side.Sell => -ShortStopLossRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        return !slRatio.IsValid() && slRatio <= 0 ? 0 : security.GetStopLossPrice(price, slRatio);
    }

    public virtual decimal GetTakeProfitPrice(decimal price, Side parentOrderSide, Security security)
    {
        decimal tpRatio = parentOrderSide switch
        {
            Side.Buy => LongTakeProfitRatio,
            Side.Sell => -ShortTakeProfitRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        return !tpRatio.IsValid() && tpRatio <= 0 ? 0 : security.GetTakeProfitPrice(price, tpRatio);
    }

    public virtual async Task<ExternalQueryState> Open(AlgoEntry current, AlgoEntry last, decimal price, Side enterSide, DateTime time)
    {
        var sl = GetStopLossPrice(price, enterSide, current.Security);
        var tp = GetTakeProfitPrice(price, enterSide, current.Security);
        return await Entering.Open(current, last, price, enterSide, time, sl, tp);
    }

    public abstract Task<ExternalQueryState> Close(AlgoEntry current, Security security, decimal triggerPrice, Side exitSide, DateTime exitTime, OrderActionType actionType);

    public abstract Task<ExternalQueryState> CloseByTickStopLoss(AlgoEntry current, Asset position, decimal triggerPrice);

    public abstract Task<ExternalQueryState> CloseByTickTakeProfit(AlgoEntry current, Asset position, decimal triggerPrice);

    public abstract string PrintAlgorithmParameters();
}