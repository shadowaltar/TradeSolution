using Common;
using log4net;
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
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public abstract class Algorithm
{
    private static readonly ILog _log = Logger.New();

    protected WorkingItemMonitor<Asset> _closingPositionMonitor;
    private readonly Context _context;

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

    protected Algorithm(Context context)
    {
        _closingPositionMonitor = new WorkingItemMonitor<Asset>();
        _context = context;
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
        var state = await Entering.Open(current, last, price, enterSide, time, sl, tp);
        if (state.Content is Order order)
        {
            if (order.Side == Side.Buy)
                current.LongQuantity = order.Quantity;
            else if (order.Side == Side.Sell)
                current.ShortQuantity = order.Quantity;

            current.TheoreticEnterTime = time;
            current.TheoreticEnterPrice = order.Price;
            current.SequenceId = IdGenerators.Get<AlgoEntry>().NewInt;
        }
        return state;
    }

    public virtual async Task<ExternalQueryState> Close(AlgoEntry current, Security security, decimal triggerPrice, Side exitSide, DateTime time, OrderActionType actionType)
    {
        var asset = _context.Services.Algo.GetAsset(current);
        if (asset == null)
        {
            _log.Warn($"Other logic has closed the position for security {security.Code} already OR it did not exist; algo-crossing close logic is skipped.");
            return ExternalQueryStates.CloseConflict(security.Code);
        }
        if (_closingPositionMonitor.MonitorAndPreventOtherActivity(asset))
        {
            _log.Warn($"Other logic is closing the position for security {security.Code} already; algo-crossing close logic is skipped.");
            return ExternalQueryStates.CloseConflict(security.Code);
        }
        var state = await Exiting.Close(current, security, triggerPrice, exitSide, time, actionType);
        if (state.Content is Order order)
        {
            if (exitSide == Side.Sell)
                current.LongCloseType = CloseType.None;
            else if (exitSide == Side.Buy)
                current.ShortCloseType = CloseType.None;
            
            current.TheoreticExitPrice = order.Price;
            current.TheoreticExitTime = time;
            current.SequenceId = 0;
        }
        return state;
    }

    public abstract Task<ExternalQueryState> CloseByTickStopLoss(AlgoEntry current, Asset position, decimal triggerPrice);

    public abstract Task<ExternalQueryState> CloseByTickTakeProfit(AlgoEntry current, Asset position, decimal triggerPrice);

    public abstract string PrintAlgorithmParameters();
}