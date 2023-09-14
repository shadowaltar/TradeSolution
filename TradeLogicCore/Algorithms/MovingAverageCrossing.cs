using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public class MovingAverageCrossing : IAlgorithm<MacVariables>
{
    private static readonly ILog _log = Logger.New();

    private readonly SimpleMovingAverage _fastMa;
    private readonly SimpleMovingAverage _slowMa;
    private readonly Context _context;

    public IAlgorithmEngine<MacVariables> Engine { get; }

    public int FastParam { get; } = 2;
    public int SlowParam { get; } = 5;
    public decimal StopLossRatio { get; } = 0.02m;

    public IPositionSizingAlgoLogic Sizing { get; }
    public IEnterPositionAlgoLogic Entering { get; }
    public IExitPositionAlgoLogic Exiting { get; }
    public ISecurityScreeningAlgoLogic Screening { get; set; }

    private readonly OpenPositionPercentageFeeLogic _upfrontFeeLogic;

    public MovingAverageCrossing(Context context,
                                 int fast,
                                 int slow,
                                 decimal stopLossRatio = decimal.MinValue,
                                 decimal takeProfitRatio = decimal.MinValue,
                                 IPositionSizingAlgoLogic? sizing = null,
                                 ISecurityScreeningAlgoLogic? screening = null,
                                 IEnterPositionAlgoLogic? entering = null,
                                 IExitPositionAlgoLogic? exiting = null)
    {
        _context = context;

        _upfrontFeeLogic = new OpenPositionPercentageFeeLogic();
        Sizing = sizing ?? new SimplePositionSizing();
        Screening = screening ?? new SimpleSecurityScreeningAlgoLogic();
        Entering = entering ?? new SimpleEnterPositionAlgoLogic(_context);
        Exiting = exiting ?? new SimpleExitPositionAlgoLogic(_context, stopLossRatio, takeProfitRatio);
        FastParam = fast;
        SlowParam = slow;
        StopLossRatio = stopLossRatio;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new SimpleMovingAverage(SlowParam, "SLOW SMA");
    }

    public void BeforeProcessingSecurity(IAlgorithmEngine<MacVariables> context, Security security)
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

    public bool IsOpenLongSignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        if (lastPrice == null)
            return false;

        ProcessSignal(current, last);

        return current.Variables.PriceXFast > 0
               && current.Variables.PriceXSlow > 0
               && current.Variables.FastXSlow > 0;
    }

    public bool IsCloseLongSignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last, OhlcPrice currentPrice, OhlcPrice? lastPrice)
    {
        if (lastPrice == null)
            return true;

        ProcessSignal(current, last);

        return current.Variables.PriceXFast < 0
               && current.Variables.PriceXSlow < 0
               && current.Variables.FastXSlow < 0;
    }

    public void AfterLongClosed(AlgoEntry<MacVariables> entry)
    {
        ResetInheritedVariables(entry);
    }

    public void AfterStopLossLong(AlgoEntry<MacVariables> entry)
    {
        ResetInheritedVariables(entry);
    }

    private void ProcessSignal(AlgoEntry<MacVariables> current, AlgoEntry<MacVariables> last)
    {
        if (TryCheck(last.Price, current.Price, last.Variables.Fast, current.Variables.Fast, out var pxf))
        {
            current.Variables.PriceXFast = pxf;
        }

        if (TryCheck(last.Price, current.Price, last.Variables.Slow, current.Variables.Slow, out var pxs))
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
        entry.Variables.FastXSlow = 0;
    }

    public static bool TryCheck(decimal last1, decimal current1, decimal last2, decimal current2, out int crossing)
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

    public string Type => "MovingAverageCrossing";
}