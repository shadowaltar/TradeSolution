using Common;
using TradeCommon.Algorithms;
using TradeCommon.Calculations;
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

public class Rumi : Algorithm
{
    private readonly SimpleMovingAverage _fastMa;
    private readonly ExponentialMovingAverageV2 _slowMa;
    private readonly SimpleMovingAverage _rumiMa;
    private readonly Context _context;

    public override AlgorithmParameters AlgorithmParameters { get; }

    public int Id => 2;

    public int VersionId => 20230801;

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public int RumiParam { get; } = 1;
    public bool AllowMultipleOpenOrders => false;

    public override IPositionSizingAlgoLogic Sizing { get; set; }
    public override IEnterPositionAlgoLogic Entering { get; set; }
    public override IExitPositionAlgoLogic Exiting { get; set; }
    public override ISecurityScreeningAlgoLogic Screening { get; set; }

    public override decimal LongStopLossRatio { get; }

    public override decimal LongTakeProfitRatio { get; }

    public override decimal ShortStopLossRatio { get; }

    public override decimal ShortTakeProfitRatio { get; }

    public override bool IsShortSellAllowed { get; }

    public Rumi(Context context, int fast, int slow, int rumi, decimal stopLossRatio, bool isShortSellAllowed = false)
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
        IsShortSellAllowed = isShortSellAllowed;
        ShortStopLossRatio = stopLossRatio;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new ExponentialMovingAverageV2(SlowParam, 2, "SLOW EMA");
        _rumiMa = new SimpleMovingAverage(RumiParam, "RUMI SMA");
    }

    public decimal GetSize(decimal availableCash, AlgoEntry current, AlgoEntry last, decimal price, DateTime time)
    {
        return Sizing.GetSize(availableCash, current, last, price, time);
    }

    public override IAlgorithmVariables CalculateVariables(decimal price, AlgoEntry? last)
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

    public override void Analyze(AlgoEntry current, AlgoEntry last, OhlcPrice currentPrice, OhlcPrice lastPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables).Rumi;
        var rumi = ((RumiVariables)current.Variables).Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;
        current.LongSignal = isSignal ? SignalType.Open : SignalType.None;
    }

    public override bool CanOpenLong(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (openOrders.IsNullOrEmpty() && current.LongCloseType == CloseType.None && current.LongSignal == SignalType.Open)
            return true;
        return false;
    }

    public override bool CanOpenShort(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        if (openOrders.IsNullOrEmpty() && current.ShortCloseType == CloseType.None && current.ShortSignal == SignalType.Open)
            return true;
        return false;
    }

    public override bool CanCloseLong(AlgoEntry current)
    {
        var position = _context.Services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        return position != null
               && position.Side == Side.Buy
               && current.LongCloseType == CloseType.None
               && current.LongSignal == SignalType.Close;
    }

    public override bool CanCloseShort(AlgoEntry current)
    {
        var position = _context.Services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        return position != null
               && position.Side == Side.Sell
               && current.ShortCloseType == CloseType.None
               && current.ShortSignal == SignalType.Close;
    }

    public override bool CanCancel(AlgoEntry current)
    {
        var openOrders = _context.Services.Order.GetOpenOrders(current.Security);
        return !openOrders.IsNullOrEmpty();
    }

    public override bool ShallStopLoss(int securityId, Tick tick)
    {
        throw new NotImplementedException();
    }

    public override bool ShallTakeProfit(int securityId, Tick tick)
    {
        throw new NotImplementedException();
    }

    public override Task<ExternalQueryState> Close(AlgoEntry current, Security security, Side exitSide, DateTime exitTime)
    {
        throw new NotImplementedException();
    }

    public override Task<ExternalQueryState> CloseByTickStopLoss(Position position)
    {
        throw new NotImplementedException();
    }

    public override Task<ExternalQueryState> CloseByTickTakeProfit(Position position)
    {
        throw new NotImplementedException();
    }
}

public record RumiVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;
    public decimal Rumi { get; set; } = decimal.MinValue;
    public decimal LastRumi { get; set; } = decimal.MinValue;

    public string Format(Security security)
    {
        return $"F:{FormatPrice(Fast, security)}, S:{FormatPrice(Slow, security)}, Rumi:{FormatPrice(Rumi, security)}";

        static string FormatPrice(decimal price, Security security)
        {
            return price.IsValid() ? security.RoundTickSize(price).ToString() : "N/A";
        }
    }

    public override string ToString()
    {
        return $"F:{Fast.NAIfInvalid("F16")}, S:{Slow.NAIfInvalid("F16")}, Rumi:{Rumi.NAIfInvalid("F16")}";
    }
}