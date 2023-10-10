﻿using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public class Rumi : IAlgorithm<RumiVariables>
{
    private static readonly ILog _log = Logger.New();

    private readonly SimpleMovingAverage _fastMa;
    private readonly ExponentialMovingAverageV2 _slowMa;
    private readonly SimpleMovingAverage _rumiMa;

    public int Id => 2;

    public int VersionId => 20230801;

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public int RumiParam { get; } = 1;

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

    public bool IsLongSignal(AlgoEntry<RumiVariables> current, AlgoEntry<RumiVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        var lastRumi = last.Variables!.Rumi;
        var rumi = current.Variables!.Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;

        //if (isSignal && current.Variables.Fast < currentPrice.C)
        //{
        //    return -1;
        //}

        return isSignal;
    }

    public bool IsCloseLongSignal(AlgoEntry<RumiVariables> current, AlgoEntry<RumiVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        var lastRumi = last.Variables!.Rumi;
        var rumi = current.Variables!.Rumi;
        var isSignal = lastRumi.IsValid() && rumi.IsValid() && lastRumi > 0 && rumi < 0;
        return isSignal;
    }

    public void ValidateSignal(int signal1, int signal2)
    {
        Assertion.ShallNever(signal1 == 1 && signal2 == 1);
        Assertion.ShallNever(signal1 == -1 && signal2 == -1);
    }
}

public record RumiVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;
    public decimal Rumi { get; set; } = decimal.MinValue;
    public decimal LastRumi { get; set; } = decimal.MinValue;
}