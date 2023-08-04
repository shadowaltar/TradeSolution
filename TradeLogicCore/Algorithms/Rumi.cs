using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using static Futu.OpenApi.Pb.TrdCommon;
using static TradeLogicCore.Algorithms.Rumi;

namespace TradeLogicCore.Algorithms;
public class Rumi : AbstractAlgorithm<RumiVariables>
{
    private static readonly ILog _log = Logger.New();

    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    private readonly SimpleMovingAverage _fastMa;
    private readonly ExponentialMovingAverage _slowMa;
    private readonly SimpleMovingAverage _rumiMa;

    private DateTime _backTestStartTime;



    public int FastParam { get; set; } = 3;
    public int SlowParam { get; set; } = 5;
    public int RumiParam { get; set; } = 3;

    public decimal StopLossRatio { get; set; } = 0;

    public decimal InitialFreeCash { get; private set; }
    public decimal TotalRealizedPnl { get; private set; }
    public decimal FreeCash { get; private set; }

    public Rumi(IHistoricalMarketDataService historicalMarketDataService)
    {
        _historicalMarketDataService = historicalMarketDataService;

        _fastMa = new SimpleMovingAverage(FastParam, "FAST SMA");
        _slowMa = new ExponentialMovingAverage(SlowParam, 2, "SLOW EMA");
        _rumiMa = new SimpleMovingAverage(RumiParam, "RUMI SMA");
    }

    public IPositionSizingAlgoLogic Sizing => throw new NotImplementedException();

    public IEnterPositionAlgoLogic Entering => throw new NotImplementedException();

    public IExitPositionAlgoLogic Exiting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic SecuritySelecting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic Screening => throw new NotImplementedException();

    public bool IsLoggingEnabled { get; set; }

    public async Task<List<RuntimePosition<RumiVariables>>> BackTest(Security security, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1)
    {
        _backTestStartTime = start;
        InitialFreeCash = initialCash;
        FreeCash = initialCash;

        var ts = IntervalTypeConverter.ToTimeSpan(intervalType).TotalDays;

        var prices = await _historicalMarketDataService.Get(security, intervalType, start, end);
        if (prices.Count == 0)
        {

        }
        RuntimePosition<RumiVariables>? lastPosition = null;
        var entries = new List<RuntimePosition<RumiVariables>>(prices.Count);
        OhlcPrice? lastOhlcPrice = null;
        foreach (OhlcPrice? ohlcPrice in prices)
        {
            var position = new RuntimePosition<RumiVariables>();
            var price = OhlcPrice.PriceElementSelectors[PriceElementType.Close](ohlcPrice);
            var low = ohlcPrice.L;

            var algorithmVariables = CalculateVariables(price, lastPosition) as RumiVariables;
            position.Variables = algorithmVariables;
            position.Price = price;
            position.FreeCash = FreeCash;

            // copy over most of the states from exitPrice to this
            if (lastPosition != null && !lastPosition.IsClosing)
            {
                CopyPosition(position, lastPosition, price);
            }

            // mimic stop loss
            if (position.IsOpened && low <= position.StopLossPrice)
            {
                // assuming always stopped loss at the triggering market stopLossPrice
                StopLoss(position, lastPosition!, GetOhlcEndTime(ohlcPrice, intervalType));
                TotalRealizedPnl += position.RealizedPnl;
            }

            Assertion.ShallNever(lastPosition != null && lastPosition.StopLossPrice == 0 && lastPosition.IsOpened);

            // try open or currentPrice a current
            // assuming no margin short-sell is allowed
            if (lastPosition != null)
            {
                position.IsLongSignal = IsLongSignal(position, lastPosition, ohlcPrice);
                position.IsShortSignal = IsShortSignal(position, lastPosition, ohlcPrice);
                if (!position.IsOpened && position.IsLongSignal)
                {
                    // calculate SL and try to round-up
                    OpenPosition(position, lastPosition, price, GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));
                }
                if (position.IsOpened && position.IsShortSignal)
                {
                    ClosePosition(position, price, GetOhlcEndTime(ohlcPrice, intervalType));
                    TotalRealizedPnl += position.RealizedPnl;
                }
            }

            if (FirstPosition == null)
            {
                position.FreeCash = initialCash;
                position.Notional = initialCash;
                FirstPosition = position;
            }
            lastPosition = position;
            lastOhlcPrice = ohlcPrice;
            entries.Add(position);
        }

        if (lastPosition != null && lastPosition.IsOpened)
        {
            _log.Info("Attempt to close current at the end of back-testing.");
            ClosePosition(lastPosition, lastOhlcPrice?.C ?? 0, GetOhlcEndTime(lastOhlcPrice, intervalType));
            TotalRealizedPnl += lastPosition.RealizedPnl;
        }

        Assertion.Shall((InitialFreeCash + TotalRealizedPnl).ApproxEquals(FreeCash));

        return entries;

        DateTime GetOhlcEndTime(OhlcPrice? price, IntervalType intervalType)
        {
            if (price == null)
                return DateTime.MinValue;
            return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
        }

        decimal GetStopLoss(OhlcPrice price, Security security)
        {
            return decimal.Round(price.C * (1 - StopLossRatio), security.PriceDecimalPoints, MidpointRounding.ToPositiveInfinity);
        }
    }

    protected override RumiVariables CalculateVariables(decimal price, RuntimePosition<RumiVariables>? last)
    {
        var variables = new RumiVariables();
        var lastVariables = last == null ? null : last.Variables as RumiVariables;
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

    protected override bool IsLongSignal(RuntimePosition<RumiVariables> current, RuntimePosition<RumiVariables> last, OhlcPrice ohlcPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables!).Rumi;
        var rumi = ((RumiVariables)current.Variables!).Rumi;
        return lastRumi.IsValid() && rumi.IsValid() && lastRumi < 0 && rumi > 0;
    }

    protected override bool IsShortSignal(RuntimePosition<RumiVariables> current, RuntimePosition<RumiVariables> last, OhlcPrice ohlcPrice)
    {
        var lastRumi = ((RumiVariables)last.Variables!).Rumi;
        var rumi = ((RumiVariables)current.Variables!).Rumi;
        return lastRumi.IsValid() && rumi.IsValid() && lastRumi > 0 && rumi < 0;
    }

    protected override void OpenPosition(RuntimePosition<RumiVariables> current, RuntimePosition<RumiVariables> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice)
    {
        Assertion.ShallNever(last.FreeCash == 0);

        current.IsOpened = true;
        current.IsClosing = false;
        // TODO current sizing happens here
        current.Quantity = FreeCash / enterPrice;
        current.EnterPrice = enterPrice;
        current.EnterTime = enterTime;
        current.ExitPrice = 0;
        current.ExitTime = DateTime.MinValue;
        current.StopLossPrice = stopLossPrice;
        current.IsStopLossTriggered = false;
        current.FreeCash = 0;
        current.Notional = current.Quantity * enterPrice; // this does not indicates the free money
        current.RealizedPnl = 0;
        current.UnrealizedPnl = 0;
        current.OrderReturn = 0;

        _log.Info($"Opened current: [{current.EnterTime:yyMMdd-HHmm}] [{current.EnterPrice:F2}*{current.Quantity:F2}] SL[{current.StopLossPrice:F2}]");
        FreeCash -= current.Notional;
    }

    protected override void ClosePosition(RuntimePosition<RumiVariables> current, decimal exitPrice, DateTime exitTime)
    {
        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        current.IsOpened = false;
        current.IsClosing = true;
        current.ExitPrice = exitPrice;
        current.ExitTime = exitTime;
        current.IsStopLossTriggered = false;
        current.RealizedPnl = (exitPrice - current.EnterPrice) * current.Quantity;
        current.Notional = current.Quantity * exitPrice;
        current.UnrealizedPnl = 0;
        current.OrderReturn = (exitPrice - current.EnterPrice) / current.EnterPrice;
        current.FreeCash = current.Notional;
        _log.Info($"Close:[{current.sec}] [{current.EnterTime:yyMMdd-HHmm}] [({current.ExitPrice:F2}-{current.EnterPrice:F2})*{current.Quantity:F2}={current.RealizedPnl:F2}][{current.OrderReturn:P2}] SL[{current.StopLossPrice:F2}]");

        Assertion.ShallNever(current.OrderReturn < StopLossRatio * -1);
        Assertion.ShallNever(current.FreeCash == 0);

        FreeCash += current.Notional;
    }

    protected override void StopLoss(RuntimePosition<RumiVariables> current, RuntimePosition<RumiVariables> last, DateTime exitTime)
    {
        current.IsOpened = false;
        current.IsClosing = true;
        current.ExitPrice = current.StopLossPrice;
        current.ExitTime = exitTime;
        current.IsStopLossTriggered = true;
        current.UnrealizedPnl = 0;
        current.RealizedPnl = (current.StopLossPrice - current.EnterPrice) * current.Quantity;
        current.Notional = current.Quantity * current.StopLossPrice;
        current.OrderReturn = (current.StopLossPrice - current.EnterPrice) / current.EnterPrice;
        current.FreeCash = current.Notional;
        _log.Info($"StopLossed: [{current.EnterTime:yyMMdd-HHmm}] [({current.StopLossPrice:F2}-{current.EnterPrice:F2})*{current.Quantity:F2}={current.RealizedPnl:F2}][{current.OrderReturn:P2}] SL[{current.StopLossPrice:F2}]");

        Assertion.ShallNever(current.OrderReturn < StopLossRatio * -1);

        FreeCash += current.Notional;
    }

    protected override void CopyPosition(RuntimePosition<RumiVariables> current, RuntimePosition<RumiVariables> last, decimal currentPrice)
    {
        current.IsOpened = last.IsOpened;
        if (current.IsOpened)
        {
            current.Quantity = last.Quantity;
            current.EnterPrice = last.EnterPrice;
            current.EnterTime = last.EnterTime;
            current.ExitPrice = last.ExitPrice;
            current.ExitTime = last.ExitTime;
            current.StopLossPrice = last.StopLossPrice;
            current.IsStopLossTriggered = false;
            current.FreeCash = last.FreeCash;
            current.UnrealizedPnl = (currentPrice - current.EnterPrice) * current.Quantity;
            current.RealizedPnl = 0;
            current.Notional = current.Quantity * current.EnterPrice + current.UnrealizedPnl;
        }
        else
        {
            current.Quantity = 0;
            current.EnterPrice = 0;
            current.EnterTime = DateTime.MinValue;
            current.ExitPrice = 0;
            current.ExitTime = DateTime.MinValue;
            current.StopLossPrice = 0;
            current.IsStopLossTriggered = false;
            current.FreeCash = last.FreeCash;
            current.UnrealizedPnl = 0;
            current.RealizedPnl = 0;
            current.Notional = current.FreeCash;
        }
    }
}


public class RumiVariables : IAlgorithmVariables
{
    public decimal Fast { get; set; } = decimal.MinValue;
    public decimal Slow { get; set; } = decimal.MinValue;
    public decimal Rumi { get; set; } = decimal.MinValue;
    public decimal LastRumi { get; set; } = decimal.MinValue;
}


public record RuntimePosition<T>
{
    public T? Variables { get; set; }

    public decimal Price { get; set; } = decimal.MinValue;

    public bool IsLongSignal { get; set; }
    public bool IsShortSignal { get; set; }
    public bool IsOpened { get; set; }
    public bool IsClosing { get; set; }

    public decimal Quantity { get; set; }
    public decimal EnterPrice { get; set; }
    public DateTime EnterTime { get; set; }
    public decimal ExitPrice { get; set; }
    public DateTime ExitTime { get; set; }

    public decimal StopLossPrice { get; set; }
    public bool IsStopLossTriggered { get; set; }

    public decimal FreeCash { get; set; }
    public decimal Notional { get; set; }
    public decimal OrderReturn { get; set; }
    public decimal UnrealizedPnl { get; set; }
    public decimal RealizedPnl { get; set; }
}