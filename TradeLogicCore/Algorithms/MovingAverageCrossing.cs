using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public class MovingAverageCrossing : IAlgorithm<MacVariables>
{
    private static readonly ILog _log = Logger.New();

    private readonly SimpleMovingAverage _fastMa;
    private readonly SimpleMovingAverage _slowMa;

    public IAlgorithemContext<MacVariables> Context { get; set; }

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public decimal StopLossRatio { get; } = 0.02m;

    public IPositionSizingAlgoLogic<MacVariables> Sizing { get; }
    public IEnterPositionAlgoLogic<MacVariables> Entering { get; }
    public IExitPositionAlgoLogic<MacVariables> Exiting { get; }
    public ISecurityScreeningAlgoLogic<MacVariables> Screening { get; }

    public MovingAverageCrossing(AlgorithmEngine<MacVariables> engine, int fast, int slow, decimal stopLossRatio)
    {
        Sizing = new SimplePositionSizing<MacVariables>();
        Screening = new SingleSecurityScreeningAlgoLogic<MacVariables>();
        Entering = new SimpleEnterPositionAlgoLogic<MacVariables>(engine, Sizing);
        Exiting = new SimpleExitPositionAlgoLogic<MacVariables>(engine, stopLossRatio);

        FastParam = fast;
        SlowParam = slow;
        StopLossRatio = stopLossRatio;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new SimpleMovingAverage(SlowParam, "SLOW SMA");
    }

    public decimal GetSize(decimal availableCash, AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, decimal price, DateTime time)
    {
        return Sizing.GetSize(availableCash, current, last, price, time);
    }

    public MacVariables CalculateVariables(decimal price, AlgoEntry<MacVariables>? last)
    {
        var variables = new MacVariables();
        var lastVariables = last?.Variables;

        var fast = _fastMa.Next(price);
        var slow = _slowMa.Next(price);

        variables.Fast = fast;
        variables.Slow = slow;

        // these two flags need to be inherited
        variables.PriceXFast = lastVariables?.PriceXFast ?? 0;
        variables.PriceXSlow = lastVariables?.PriceXSlow ?? 0;
        variables.FastXSlow = lastVariables?.FastXSlow ?? 0;

        return variables;
    }

    public int IsBuySignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        if (lastPrice == null)
            return 0;

        ProcessSignal(current, last, currentPrice, lastPrice);

        if (current.Variables.PriceXFast == 1 && current.Variables.PriceXSlow == 1 && current.Variables.FastXSlow == 1)
            return 1;
        return 0;
    }

    public int IsSellCloseSignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        if (lastPrice == null)
            return 0;

        ProcessSignal(current, last, currentPrice, lastPrice);

        if (current.Variables.PriceXFast == -1 && current.Variables.PriceXSlow == -1)
            return 1;
        return 0;
    }

    public void AfterSellClose(AlgoEntry<MacVariables> entry) => ResetInheritedVariables(entry);

    public void AfterBuyStopLoss(AlgoEntry<MacVariables> entry) => ResetInheritedVariables(entry);

    private static void ProcessSignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        if (lastPrice == null)
            return;

        if (TryCheck(lastPrice.C, currentPrice.C, last.Variables.Fast, current.Variables.Fast, out var pxf))
        {
            current.Variables.PriceXFast = pxf;
        }

        if (TryCheck(lastPrice.C, currentPrice.C, last.Variables.Slow, current.Variables.Slow, out var pxs))
        {
            current.Variables.PriceXSlow = pxs;
        }

        if (TryCheck(last.Variables.Fast, current.Variables.Fast, last.Variables.Slow, current.Variables.Slow, out var fxs))
        {
            current.Variables.FastXSlow = fxs;
        }
    }

    private static void ResetInheritedVariables(AlgoEntry<MacVariables> entry)
    {
        entry.Variables.PriceXFast = 0;
        entry.Variables.PriceXSlow = 0;
    }

    public static bool TryCheck(decimal last1, decimal current1, decimal last2, decimal current2, out int crossing)
    {
        if (last1 < last2 && current1 > current2)
        {
            // 1 cross above 2
            crossing = 1;
            return true;
        }
        else if (last1 > last2 && current1 < current2)
        {
            // 1 cross below 2
            crossing = -1;
            return true;
        }
        crossing = 0;
        return false;
    }
}

public class MacVariables : IAlgorithmVariables
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
}