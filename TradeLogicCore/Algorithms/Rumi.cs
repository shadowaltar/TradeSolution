using Common;
using log4net;
using TradeCommon.Algorithms;
using TradeCommon.Calculations;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public class Rumi : IAlgorithm
{
    private readonly SimpleMovingAverage _fastMa;
    private readonly ExponentialMovingAverageV2 _slowMa;
    private readonly SimpleMovingAverage _rumiMa;
    private readonly Context _context;

    public AlgorithmParameters AlgorithmParameters { get; set; }

    public int Id => 2;

    public int VersionId => 20230801;

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public int RumiParam { get; } = 1;
    public bool AllowMultipleOpenOrders => false;

    public IPositionSizingAlgoLogic Sizing { get; }
    public IEnterPositionAlgoLogic Entering { get; }
    public IExitPositionAlgoLogic Exiting { get; }
    public ISecurityScreeningAlgoLogic Screening { get; set; }

    public decimal LongStopLossRatio { get; set; }

    public decimal LongTakeProfitRatio { get; set; }

    public decimal ShortStopLossRatio { get; set; }

    public decimal ShortTakeProfitRatio { get; set; }

    public Rumi(Context context, int fast, int slow, int rumi, decimal stopLossRatio)
    {
        _context = context;
        Sizing = new SimplePositionSizingLogic();
        Screening = new SimpleSecurityScreeningAlgoLogic();
        Entering = new SimpleEnterPositionAlgoLogic(context);
        Exiting = new SimpleExitPositionAlgoLogic(context, stopLossRatio, decimal.MinValue);
        FastParam = fast;
        SlowParam = slow;
        RumiParam = rumi;
        LongStopLossRatio = stopLossRatio;
        ShortStopLossRatio = stopLossRatio;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new ExponentialMovingAverageV2(SlowParam, 2, "SLOW EMA");
        _rumiMa = new SimpleMovingAverage(RumiParam, "RUMI SMA");
    }

    public decimal GetSize(decimal availableCash, AlgoEntry current, AlgoEntry last, decimal price, DateTime time)
    {
        return Sizing.GetSize(availableCash, current, last, price, time);
    }

    public object CalculateVariables(decimal price, AlgoEntry? last)
    {
        var variables = new RumiVariables();
        var lastVariables = last?.Variables;
        var fast = _fastMa.Next(price);
        var slow = _slowMa.Next(price);
        var rumi = (fast.IsValid() && slow.IsValid()) ? _rumiMa.Next(fast - slow) : decimal.MinValue;
        var lastRumi = (lastVariables as RumiVariables)?.Rumi ?? decimal.MinValue;

        variables.Fast = fast;
        variables.Slow = slow;
        variables.Rumi = rumi;
        variables.LastRumi = lastRumi;

        return variables;
    }

    public bool IsLongSignal(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables).Rumi;
        var rumi = ((RumiVariables)current.Variables).Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;

        //if (isSignal && current.Variables.Fast < currentPrice.C)
        //{
        //    return -1;
        //}

        return isSignal;
    }

    public bool IsCloseLongSignal(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables).Rumi;
        var rumi = ((RumiVariables)current.Variables).Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi > 0 && rumi < 0;
        return isSignal;
    }

    public void ValidateSignal(int signal1, int signal2)
    {
        Assertion.ShallNever(signal1 == 1 && signal2 == 1);
        Assertion.ShallNever(signal1 == -1 && signal2 == -1);
    }

    public void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice lastPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables).Rumi;
        var rumi = ((RumiVariables)current.Variables).Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;
        current.LongSignal = isSignal ? SignalType.Open : SignalType.None;
    }

    public bool CanOpenLong(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (openOrders.IsNullOrEmpty() && current.LongCloseType == CloseType.None && current.LongSignal == SignalType.Open)
            return true;
        return false;
    }

    public bool CanOpenShort(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (openOrders.IsNullOrEmpty() && current.ShortCloseType == CloseType.None && current.ShortSignal == SignalType.Open)
            return true;
        return false;
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
}

public record RumiVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;
    public decimal Rumi { get; set; } = decimal.MinValue;
    public decimal LastRumi { get; set; } = decimal.MinValue;
}