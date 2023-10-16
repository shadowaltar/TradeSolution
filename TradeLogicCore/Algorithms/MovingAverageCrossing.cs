using Common;
using TradeCommon.Algorithms;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public class MovingAverageCrossing : IAlgorithm
{
    private readonly SimpleMovingAverage _fastMa;
    private readonly SimpleMovingAverage _slowMa;
    private readonly Context _context;

    /// <summary>
    /// Indicates if the security is being traded.
    /// </summary>
    private readonly HashSet<int> _tradingSecurityIds = new();

    public AlgorithmParameters AlgorithmParameters { get; set; }

    public int Id => 1;
    public int VersionId => 20230916;
    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public decimal LongStopLossRatio { get; } = 0.02m;
    public decimal LongTakeProfitRatio { get; }
    public decimal ShortStopLossRatio { get; }
    public decimal ShortTakeProfitRatio { get; }
    public IPositionSizingAlgoLogic Sizing { get; set; }
    public IEnterPositionAlgoLogic Entering { get; set; }
    public IExitPositionAlgoLogic Exiting { get; set; }
    public ISecurityScreeningAlgoLogic Screening { get; set; }


    private readonly OpenPositionPercentageFeeLogic _upfrontFeeLogic;

    public MovingAverageCrossing(Context context,
                                 int fast,
                                 int slow,
                                 decimal longStopLossRatio = decimal.MinValue,
                                 decimal longTakeProfitRatio = decimal.MinValue,
                                 decimal shortStopLossRatio = decimal.MinValue,
                                 decimal shortTakeProfitRatio = decimal.MinValue,
                                 IPositionSizingAlgoLogic? sizing = null,
                                 ISecurityScreeningAlgoLogic? screening = null,
                                 IEnterPositionAlgoLogic? entering = null,
                                 IExitPositionAlgoLogic? exiting = null)
    {
        FastParam = fast;
        SlowParam = slow;
        LongStopLossRatio = longStopLossRatio <= 0 ? decimal.MinValue : longStopLossRatio;
        LongTakeProfitRatio = longTakeProfitRatio <= 0 ? decimal.MinValue : longTakeProfitRatio;
        ShortStopLossRatio = shortStopLossRatio <= 0 ? decimal.MinValue : shortStopLossRatio;
        ShortTakeProfitRatio = shortTakeProfitRatio <= 0 ? decimal.MinValue : shortTakeProfitRatio;

        _context = context;

        _upfrontFeeLogic = new OpenPositionPercentageFeeLogic();
        Sizing = sizing ?? new SimplePositionSizingLogic();
        Screening = screening ?? new SimpleSecurityScreeningAlgoLogic();
        Entering = entering ?? new SimpleEnterPositionAlgoLogic(_context);
        Exiting = exiting ?? new SimpleExitPositionAlgoLogic(_context, longStopLossRatio, longTakeProfitRatio, shortStopLossRatio, shortTakeProfitRatio);

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new SimpleMovingAverage(SlowParam, "SLOW SMA");
    }

    public void BeforeProcessingSecurity(IAlgorithmEngine context, Security security)
    {
        if (security.Code == "ETHUSDT" && security.Exchange == ExchangeType.Binance.ToString().ToUpperInvariant())
        {
            _upfrontFeeLogic.PercentageOfQuantity = 0.001m;
            Entering.FeeLogic = _upfrontFeeLogic;
        }
        else
        {
            Entering.FeeLogic = null;
        }
    }

    public object CalculateVariables(decimal price, AlgoEntry? last)
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

    public void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice lastPrice)
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

    public bool CanOpenLong(AlgoEntry current)
    {
        // prevent trading if trading is ongoing
        if (!CanOpen(current))
            return false;

        // check long signal
        return current.LongCloseType == CloseType.None && current.LongSignal == SignalType.Open;
    }

    public void BeforeOpeningLong(AlgoEntry entry)
    {
        _tradingSecurityIds.ThreadSafeAdd(entry.SecurityId);
    }

    public void BeforeOpeningShort(AlgoEntry entry)
    {
        _tradingSecurityIds.ThreadSafeAdd(entry.SecurityId);
    }

    public bool CanOpenShort(AlgoEntry current)
    {
        // prevent trading if trading is ongoing
        if (!CanOpen(current))
            return false;

        // check short signal
        return current.ShortCloseType == CloseType.None && current.ShortSignal == SignalType.Open;
    }

    private bool CanOpen(AlgoEntry current)
    {
        // prevent trading if enter-logic is underway
        if (Entering.IsOpening)
            return false;

        // prevent trading if security is marked as being traded
        var isTrading = _tradingSecurityIds.ThreadSafeContains(current.SecurityId);
        if (isTrading)
            return false;

        // prevent trading if has open orders
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (!openOrders.IsNullOrEmpty())
            return false;

        // prevent trading if has open positions
        var hasOpenPosition = false;
        var openPosition = _context.Services.Portfolio.GetPosition(current.SecurityId);
        if (openPosition == null)
            hasOpenPosition = false;
        else if (openPosition.IsClosed && openPosition.EndTradeId > 0)
            hasOpenPosition = false;

        return !hasOpenPosition;
    }

    public bool CanCloseLong(AlgoEntry current)
    {
        var position = _context.Services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        return position != null
               && position.Side == Side.Buy
               && current.LongCloseType == CloseType.None
               && current.LongSignal == SignalType.Close;
    }

    public bool CanCloseShort(AlgoEntry current)
    {
        var position = _context.Services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        return position != null
               && position.Side == Side.Sell
               && current.ShortCloseType == CloseType.None
               && current.ShortSignal == SignalType.Close;
    }

    public bool CanCancel(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        return !openOrders.IsNullOrEmpty();
    }

    public void AfterLongClosed(AlgoEntry entry)
    {
        ResetInheritedVariables(entry);
    }

    public void AfterStopLossLong(AlgoEntry entry)
    {
        ResetInheritedVariables(entry);
    }

    public void NotifyPositionClosed(int securityId, long positionId)
    {
        _tradingSecurityIds.ThreadSafeRemove(securityId);
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
            $" LongSL% [{LongStopLossRatio}], LongTP% [{LongTakeProfitRatio}]," +
            $" ShortSL% [{ShortStopLossRatio}], ShortTP% [{ShortTakeProfitRatio}]";
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

    public override string ToString()
    {
        return $"F:{(Fast.IsValid() ? Fast.ToString("F16") : "N/A")}, S:{(Slow.IsValid()?Slow.ToString("F16"):"N/A")}, PxF:{PriceXFast}, PxS:{PriceXSlow}, FxS:{FastXSlow}";
    }
}