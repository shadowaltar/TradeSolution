using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public class Rumi : IAlgorithm<RumiVariables>
{
    private static readonly ILog _log = Logger.New();

    private readonly SimpleMovingAverage _fastMa;
    private readonly ExponentialMovingAverageV2 _slowMa;
    private readonly SimpleMovingAverage _rumiMa;

    public IAlgorithemContext<RumiVariables> Context { get; set; }

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public int RumiParam { get; } = 1;
    public decimal StopLossRatio { get; } = 0;

    public IPositionSizingAlgoLogic<RumiVariables> Sizing { get; }
    public IEnterPositionAlgoLogic<RumiVariables> Entering { get; }
    public IExitPositionAlgoLogic<RumiVariables> Exiting { get; }
    public ISecurityScreeningAlgoLogic<RumiVariables> Screening { get; }

    public Rumi(AlgorithmEngine<RumiVariables> engine, int fast, int slow, int rumi, decimal stopLossRatio)
    {
        Sizing = new SimplePositionSizing<RumiVariables>();
        Screening = new SingleSecurityScreeningAlgoLogic<RumiVariables>();
        Entering = new SimpleEnterPositionAlgoLogic<RumiVariables>(engine, Sizing);
        Exiting = new SimpleExitPositionAlgoLogic<RumiVariables>(engine, stopLossRatio);

        FastParam = fast;
        SlowParam = slow;
        RumiParam = rumi;
        StopLossRatio = stopLossRatio;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new ExponentialMovingAverageV2(SlowParam, 2, "SLOW EMA");
        _rumiMa = new SimpleMovingAverage(RumiParam, "RUMI SMA");
    }

    public decimal GetSize(decimal availableCash, AlgoEntry<RumiVariables> current, AlgoEntry<RumiVariables> last, decimal price, DateTime time)
    {
        return Sizing.GetSize(availableCash, current, last, price, time);
    }

    public RumiVariables CalculateVariables(decimal price, AlgoEntry<RumiVariables>? last)
    {
        var variables = new RumiVariables();
        var lastVariables = last?.Variables;
        var fast = _fastMa.Next(price);
        var slow = _slowMa.Next(price);
        var rumi = (fast.IsValid() && slow.IsValid()) ? _rumiMa.Next(fast - slow) : decimal.MinValue;
        var lastRumi = lastVariables?.Rumi ?? decimal.MinValue;

        variables.Fast = fast;
        variables.Slow = slow;
        variables.Rumi = rumi;
        variables.LastRumi = lastRumi;

        return variables;
    }

    public bool IsLongSignal(AlgoEntry<RumiVariables> current, AlgoEntry<RumiVariables> last, OhlcPrice ohlcPrice)
    {
        var lastRumi = last.Variables!.Rumi;
        var rumi = current.Variables!.Rumi;
        return lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;
    }

    public bool IsShortSignal(AlgoEntry<RumiVariables> current, AlgoEntry<RumiVariables> last, OhlcPrice ohlcPrice)
    {
        var lastRumi = last.Variables!.Rumi;
        var rumi = current.Variables!.Rumi;
        return lastRumi.IsValid() && rumi.IsValid() && lastRumi > 0 && rumi < 0;
    }
}

public class RumiVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;
    public decimal Rumi { get; set; } = decimal.MinValue;
    public decimal LastRumi { get; set; } = decimal.MinValue;
}