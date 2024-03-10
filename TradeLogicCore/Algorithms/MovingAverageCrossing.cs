using Common;
using log4net;
using System.Text;
using TradeCommon.Algorithms;
using TradeCommon.Calculations;
using TradeCommon.Essentials;
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

public class MovingAverageCrossing : Algorithm
{
    private static readonly ILog _log = Logger.New();

    private readonly SimpleMovingAverage _fastMa;
    private readonly SimpleMovingAverage _slowMa;
    private readonly TimeSpan _openOrderTimeout;
    private readonly Context _context;

    private readonly Dictionary<int, decimal> _stopLossPrices = [];
    private readonly Dictionary<int, decimal> _takeProfitPrices = [];

    public override AlgorithmParameters AlgorithmParameters { get; }

    public int Id => 1;
    public int VersionId => 20230916;
    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public override decimal LongStopLossRatio { get; } = 0.02m;
    public override decimal LongTakeProfitRatio { get; }
    public override decimal ShortStopLossRatio { get; }
    public override decimal ShortTakeProfitRatio { get; }
    public override IPositionSizingAlgoLogic Sizing { get; set; }
    public override IEnterPositionAlgoLogic Entering { get; set; }
    public override IExitPositionAlgoLogic Exiting { get; set; }
    public override ISecurityScreeningAlgoLogic Screening { get; set; }

    public bool IsShortSellAllowed { get; private set; }

    public MovingAverageCrossing(Context context,
                                 AlgorithmParameters parameters,
                                 int fast,
                                 int slow,
                                 decimal longStopLossRatio = decimal.MinValue,
                                 decimal longTakeProfitRatio = decimal.MinValue,
                                 decimal shortStopLossRatio = decimal.MinValue,
                                 decimal shortTakeProfitRatio = decimal.MinValue,
                                 bool isShortSellAllowed = false,
                                 IPositionSizingAlgoLogic? sizing = null,
                                 ISecurityScreeningAlgoLogic? screening = null,
                                 IEnterPositionAlgoLogic? entering = null,
                                 IExitPositionAlgoLogic? exiting = null) : base(context)
    {
        _context = context;

        FastParam = fast;
        SlowParam = slow;
        IsShortSellAllowed = isShortSellAllowed;
        LongStopLossRatio = longStopLossRatio <= 0 ? decimal.MinValue : longStopLossRatio;
        LongTakeProfitRatio = longTakeProfitRatio <= 0 ? decimal.MinValue : longTakeProfitRatio;
        ShortStopLossRatio = shortStopLossRatio <= 0 ? decimal.MinValue : shortStopLossRatio;
        ShortTakeProfitRatio = shortTakeProfitRatio <= 0 ? decimal.MinValue : shortTakeProfitRatio;
        AlgorithmParameters = parameters;

        Sizing = sizing ?? new SimplePositionSizingLogic();
        Screening = screening ?? new SimpleSecurityScreeningAlgoLogic();
        Entering = parameters.StopOrderTriggerBy == StopOrderStyleType.RealOrder
            ? entering ?? new ExternalStopOrderEnterPositionAlgoLogic(_context)
            : entering ?? new SimpleEnterPositionAlgoLogic(_context);
        Exiting = exiting ?? new SimpleExitPositionAlgoLogic(_context, longStopLossRatio, longTakeProfitRatio, shortStopLossRatio, shortTakeProfitRatio);

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new SimpleMovingAverage(SlowParam, "SLOW SMA");

        _openOrderTimeout = IntervalTypeConverter.ToTimeSpan(AlgorithmParameters.Interval) * 2;
    }

    public void BeforeProcessingSecurity(IAlgorithmEngine context, Security security)
    {
        //if (security.Code == "ETHUSDT" && security.Exchange == ExchangeType.Binance.ToString().ToUpperInvariant())
        //{
        //    _upfrontFeeLogic.PercentageOfQuantity = 0.001m;
        //    Entering.FeeLogic = _upfrontFeeLogic;
        //}
        //else
        //{
        //    Entering.FeeLogic = null;
        //}
    }

    public override IAlgorithmVariables CalculateVariables(decimal price, AlgoEntry? last)
    {
        var variables = new MacVariables();
        var lv = last?.Variables as MacVariables;

        var fast = _fastMa.Next(price);
        var slow = _slowMa.Next(price);

        variables.Fast = fast;
        variables.Slow = slow;

        // these two flags need to be inherited
        variables.PriceXFast = lv?.PriceXFast ?? 0;
        variables.PriceXSlow = lv?.PriceXSlow ?? 0;
        variables.FastXSlow = lv?.FastXSlow ?? 0;

        return variables;
    }

    /// <summary>
    /// Cancel any live or partial-filled orders which is older than
    /// two cycles.
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    public override List<Order> PickOpenOrdersToCleanUp(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security)
            .Where(o => o.Status is OrderStatus.Live or OrderStatus.PartialFilled
            && DateTime.UtcNow - o.CreateTime > _openOrderTimeout).ToList();
        return openOrders;
    }

    public override void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        ProcessSignal(current, last);

        var cv = (MacVariables)current.Variables;

        var shouldLong = cv.PriceXFast > 0
               && cv.PriceXSlow > 0
               && cv.FastXSlow > 0;

        var shouldShort = cv.PriceXFast < 0
               && cv.PriceXSlow < 0
               && cv.FastXSlow < 0;

        current.LongSignal = shouldLong ? SignalType.Open : shouldShort ? SignalType.Close : SignalType.Hold;
        current.ShortSignal = shouldShort ? SignalType.Open : shouldLong ? SignalType.Close : SignalType.Hold;
    }

    public override bool CanOpen(AlgoEntry current, Side side)
    {
        // prevent trading if trading is ongoing
        if (!CanOpen(current))
            return false;

        if (side == Side.Sell && !IsShortSellAllowed)
            return false;

        return side switch
        {
            Side.Buy => current.LongCloseType == CloseType.None && current.LongSignal == SignalType.Open,
            Side.Sell => current.ShortCloseType == CloseType.None && current.ShortSignal == SignalType.Open,
            _ => throw Exceptions.InvalidOrder("The order side to be tested is invalid: " + side),
        };
    }

    public override bool CanClose(AlgoEntry current, Side side)
    {
        CloseType closeType;
        SignalType signalType;
        switch (side)
        {
            case Side.Buy:
                closeType = current.LongCloseType;
                signalType = current.LongSignal;
                break;
            case Side.Sell:
                closeType = current.ShortCloseType;
                signalType = current.ShortSignal;
                break;
            default:
                throw Exceptions.InvalidOrder("The order side to be tested is invalid: " + side);
        }
        var asset = _context.Services.Algo.GetAsset(current);
        return asset != null
            && current.OpenSide == side
            && closeType == CloseType.None
            && signalType == SignalType.Close;
    }

    //public override bool CanOpenLong(AlgoEntry current)
    //{
    //    // prevent trading if trading is ongoing
    //    if (!CanOpen(current))
    //        return false;

    //    // check long signal
    //    return current.LongCloseType == CloseType.None && current.LongSignal == SignalType.Open;
    //}

    //public override bool CanOpenShort(AlgoEntry current)
    //{
    //    if (!IsShortSellAllowed)
    //        return false;

    //    // prevent trading if trading is ongoing
    //    if (!CanOpen(current))
    //        return false;

    //    // check short signal
    //    return current.ShortCloseType == CloseType.None && current.ShortSignal == SignalType.Open;
    //}

    public override async Task<ExternalQueryState> Open(AlgoEntry current, AlgoEntry last, decimal price, Side enterSide, DateTime time)
    {
        var state = await base.Open(current, last, price, enterSide, time);
        if (state.ResultCode == ResultCode.SendOrderOk)
        {
            var sl = GetStopLossPrice(price, enterSide, current.Security);
            _stopLossPrices.ThreadSafeSet(current.SecurityId, sl);

            var tp = GetTakeProfitPrice(price, enterSide, current.Security);
            _takeProfitPrices.ThreadSafeSet(current.SecurityId, tp);

            _log.Info($"\n\tALGO:[SETUP][{time:HHmmss}][{current.SecurityCode}]\n\t\tSLPRX:{current.Security.FormatPrice(sl)}, TPPRX:{current.Security.FormatPrice(tp)}");
        }
        return state;
    }

    public override bool ShallStopLoss(AlgoEntry current, Tick tick, out decimal triggerPrice)
    {
        triggerPrice = 0;
        var securityId = current.SecurityId;
        if (_closingPositionMonitor.IsMonitoring(securityId))
        {
            if (_log.IsDebugEnabled)
                _log.Debug($"Already working on the position (with security Id {securityId}) so cannot run StopLoss.");
            return false;
        }

        var sl = _stopLossPrices.ThreadSafeGet(securityId);
        if (sl == 0) return false; // no position or no sl is setup

        var asset = _context.Services.Algo.GetAsset(current);
        if (asset == null || asset.IsEmpty) return false;

        var originalSide = current.OpenSide;
        if (originalSide == Side.Buy && sl >= tick.Bid)
        {
            _log.Info($"\n\tALGO:[ALGO SL {current.CloseSide}][{tick.As<ExtendedTick>().Time:HHmmss}][{asset.SecurityCode}]\n\t\tPID:{asset.Id}, MID:{tick.Mid}, SLPRX:{asset.Security.FormatPrice(sl)}, QTY:{asset.Security.FormatQuantity(asset.Quantity)}");
            triggerPrice = tick.Bid;
            return true;
        }
        if (originalSide == Side.Sell && sl <= tick.Ask)
        {
            _log.Info($"\n\tALGO:[ALGO SL {current.CloseSide}][{tick.As<ExtendedTick>().Time:HHmmss}][{asset.SecurityCode}]\n\t\tPID:{asset.Id}, MID:{tick.Mid}, SLPRX:{asset.Security.FormatPrice(sl)}, QTY:{asset.Security.FormatQuantity(asset.Quantity)}");
            triggerPrice = tick.Ask;
            return true;
        }
        return false;
    }

    public override bool ShallTakeProfit(AlgoEntry current, Tick tick, out decimal triggerPrice)
    {
        triggerPrice = 0;
        var securityId = current.SecurityId;
        if (_closingPositionMonitor.IsMonitoring(securityId))
        {
            if (_log.IsDebugEnabled)
                _log.Debug($"Already working on the position (with security Id {securityId}) so cannot run TakeProfit.");
            return false;
        }

        var tp = _takeProfitPrices.ThreadSafeGet(securityId);
        if (tp == 0) return false; // no position or no tp is setup

        var asset = _context.Services.Algo.GetAsset(current);
        if (asset == null || asset.IsEmpty) return false;

        var originalSide = current.OpenSide;
        if (originalSide == Side.Buy && tp <= tick.Bid)
        {
            _log.Info($"\n\tALGO:[ALGO TP {current.CloseSide}][{tick.As<ExtendedTick>().Time:HHmmss}][{asset.SecurityCode}]\n\t\tPID:{asset.Id}, MID:{tick.Mid}, TPPRX:{asset.Security.FormatPrice(tp)}, QTY:{asset.Security.FormatQuantity(asset.Quantity)}");
            triggerPrice = tick.Bid;
            return true;
        }
        if (originalSide == Side.Sell && tp >= tick.Ask)
        {
            _log.Info($"\n\tALGO:[ALGO TP {current.CloseSide}][{tick.As<ExtendedTick>().Time:HHmmss}][{asset.SecurityCode}]\n\t\tPID:{asset.Id}, MID:{tick.Mid}, TPPRX:{asset.Security.FormatPrice(tp)}, QTY:{asset.Security.FormatQuantity(asset.Quantity)}");
            triggerPrice = tick.Ask;
            return true;
        }
        return false;
    }
    //public override bool CanCloseLong(AlgoEntry current)
    //{
    //    var position = _context.Services.Portfolio.GetAssetBySecurityId(current.SecurityId);
    //    return position != null
    //        && current.OpenSide == Side.Buy
    //        && current.LongCloseType == CloseType.None
    //        && current.LongSignal == SignalType.Close;
    //}

    //public override bool CanCloseShort(AlgoEntry current)
    //{
    //    if (!IsShortSellAllowed)
    //        return false;
    //    var position = _context.Services.Portfolio.GetAssetBySecurityId(current.SecurityId);
    //    return position != null
    //        && current.OpenSide == Side.Sell
    //        && current.ShortCloseType == CloseType.None
    //        && current.ShortSignal == SignalType.Close;
    //}

    public override bool CanCancel(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        return !openOrders.IsNullOrEmpty();
    }

    public override async Task<ExternalQueryState> CloseByTickStopLoss(AlgoEntry current, Asset asset, decimal triggerPrice)
    {
        current.SequenceId = 0;
        var state = await CloseByTick(current, asset, OrderActionType.TickSignalStopLoss, triggerPrice);

        if (state.Content is Order order)
        {
            current.LongCloseType = CloseType.None;
            current.ShortCloseType = CloseType.None;
        }
        return state;
    }

    public override async Task<ExternalQueryState> CloseByTickTakeProfit(AlgoEntry current, Asset asset, decimal triggerPrice)
    {
        current.SequenceId = 0;
        var state = await CloseByTick(current, asset, OrderActionType.TickSignalTakeProfit, triggerPrice);

        if (state.Content is Order order)
        {
            current.LongCloseType = CloseType.None;
            current.ShortCloseType = CloseType.None;
        }
        return state;
    }

    public override async Task<ExternalQueryState> Close(AlgoEntry current, Security security, decimal triggerPrice, Side exitSide, DateTime exitTime, OrderActionType actionType)
    {
        return await base.Close(current, security, triggerPrice, exitSide, exitTime, actionType);
    }

    public override void AfterPositionChanged(AlgoEntry current)
    {
        var asset = _context.Services.Algo.GetAsset(current);
        if (asset != null && asset.IsEmpty)
        {
            ResetInheritedVariables(current);

            _closingPositionMonitor.MarkAsDone(current.SecurityId);
        }
        else if (asset == null)
        {
            // impossible
            _log.Error("Asset is missing when it is changed!");
        }
    }

    public override void AfterStoppedLoss(AlgoEntry entry)
    {
        ResetInheritedVariables(entry);
    }

    public override void AfterTookProfit(AlgoEntry entry)
    {
        ResetInheritedVariables(entry);
    }

    private bool CanOpen(AlgoEntry current)
    {
        // prevent trading if enter-logic is underway
        if (Entering.IsOpening)
            return false;

        // prevent trading if has open orders
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (!openOrders.IsNullOrEmpty())
            return false;

        // prevent trading if has open positions
        var hasOpenPosition = false;
        var asset = _context.Services.Algo.GetAsset(current);
        if (asset != null && !asset.IsEmpty)
            hasOpenPosition = true;

        return !hasOpenPosition;
    }

    private async Task<ExternalQueryState> CloseByTick(AlgoEntry current, Asset position, OrderActionType actionType, decimal triggerPrice)
    {
        if (_closingPositionMonitor.MonitorAndPreventOtherActivity(position))
        {
            _log.Warn($"Other logic is closing the position for security {position.SecurityCode} already; tick-triggered close logic is skipped.");
            return ExternalQueryStates.CloseConflict(position.SecurityCode);
        }
        var state = await Exiting.Close(current, position.Security, triggerPrice, current.CloseSide, DateTime.UtcNow, actionType);

        if (state.Content is Order order)
        {
            current.SequenceId = 0;
            current.LongCloseType = CloseType.None;
            current.ShortCloseType = CloseType.None;
            current.TheoreticExitPrice = order.Price;
        }
        return state;
    }

    private static void ProcessSignal(AlgoEntry current, AlgoEntry last)
    {
        var lv = (MacVariables)last.Variables;
        var cv = (MacVariables)current.Variables;
        if (CheckCrossing(last.Price, current.Price, lv.Fast, cv.Fast, out var pxf))
        {
            cv.PriceXFast = pxf;
        }

        if (CheckCrossing(last.Price, current.Price, lv.Slow, cv.Slow, out var pxs))
        {
            cv.PriceXSlow = pxs;
        }

        if (CheckCrossing(lv.Fast, cv.Fast, lv.Slow, cv.Slow, out var fxs))
        {
            cv.FastXSlow = fxs;
        }
    }

    private static void ResetInheritedVariables(AlgoEntry current)
    {
        var cv = (MacVariables)current.Variables;
        cv.PriceXFast = 0;
        cv.PriceXSlow = 0;
        cv.FastXSlow = 0;
    }

    private static bool CheckCrossing(decimal last1, decimal current1, decimal last2, decimal current2, out int crossing)
    {
        if (!last1.IsValid() || !last2.IsValid() || !current1.IsValid() || !current2.IsValid())
        {
            crossing = 0;
            return false;
        }

        if (last1 < last2 && current1 > current2)
        {
            // ASC: 1 cross from below to above 2
            crossing = 1;
            return true;
        }
        else if (last1 > last2 && current1 < current2)
        {
            // DESC: 1 cross from above to below 2
            crossing = -1;
            return true;
        }
        else if (last1 == last2 && current1 > current2)
        {
            // Half-ASC: was equal, then 1 goes above 2
            crossing = 2;
            return true;
        }
        else if (last1 == last2 && current1 < current2)
        {
            // Half-DESC: was equal, then 1 goes below 2
            crossing = 2;
            return true;
        }

        // case == & == is treated as no-change
        // case > & >, < & < are treated as no-change
        // case > & =, < & = are treated as no-change
        crossing = 0;
        return false;
    }

    public override string ToString()
    {
        return $"Algo - Moving Average Crossing: Fast [{_fastMa.GetType().Name}:{FastParam}], Slow [{_slowMa.GetType().Name}:{SlowParam}]," +
            $" LongSL% [{LongStopLossRatio.NAIfInvalid()}], LongTP% [{LongTakeProfitRatio.NAIfInvalid()}]," +
            $" ShortSL% [{ShortStopLossRatio.NAIfInvalid()}], ShortTP% [{ShortTakeProfitRatio.NAIfInvalid()}]";
    }

    public override string PrintAlgorithmParameters()
    {
        var sb = new StringBuilder();
        sb.Append("\"FastMA\":\"").Append(FastParam).AppendLine("\",");
        sb.Append("\"SlowMA\":\"").Append(SlowParam).AppendLine("\",");
        return sb.ToString();
    }
}

public record MacVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;

    /// <summary>
    /// Flag == 1 when Close crossed from below Fast to above Fast once previously.
    /// -1 the other way round. 0 means no crossing after the flag is reset.
    /// The flag is meant to be inherited in next price cycle until position is opened/closed.
    /// </summary>
    public int PriceXFast { get; set; } = 0;

    /// <summary>
    /// Flag == 1 when Close crossed from below Slow to above Slow once previously.
    /// -1 the other way round. 0 means no crossing after the flag is reset.
    /// The flag is meant to be inherited in next price cycle until position is opened/closed.
    /// </summary>
    public int PriceXSlow { get; set; } = 0;

    /// <summary>
    /// Flag == 1 when Fast crossed from below Slow to above Slow once previously.
    /// -1 the other way round. 0 means no crossing after the flag is reset.
    /// </summary>
    public int FastXSlow { get; set; } = 0;

    public string Format(Security security)
    {
        return $"F:{FormatPrice(Fast, security)}, S:{FormatPrice(Slow, security)}, PxF:{PriceXFast}, PxS:{PriceXSlow}, FxS:{FastXSlow}";

        static string FormatPrice(decimal price, Security security) => price.IsValid() ? security.RoundTickSize(price).ToString() : "N/A";
    }

    public override string ToString()
    {
        return $"F:{Fast.NAIfInvalid("F16")}, S:{Slow.NAIfInvalid("F16")}, PxF:{PriceXFast}, PxS:{PriceXSlow}, FxS:{FastXSlow}";
    }
}