using Common;
using log4net;
using TradeCommon.Calculations;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public class Rumi : IAlgorithm
{
    private static readonly ILog _log = Logger.New();

    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    private RumiPosition? _firstPosition;
    private DateTime _backTestStartTime;

    private decimal _freeCash;

    public int FastParam { get; set; } = 3;
    public int SlowParam { get; set; } = 5;
    public int RumiParam { get; set; } = 3;

    public decimal StopLossRatio { get; set; } = 0;
    public decimal FreeCash => _freeCash;

    public Rumi(IHistoricalMarketDataService historicalMarketDataService)
    {
        _historicalMarketDataService = historicalMarketDataService;
    }

    public IPositionSizingAlgoLogic Sizing => throw new NotImplementedException();

    public IEnterPositionAlgoLogic Entering => throw new NotImplementedException();

    public IExitPositionAlgoLogic Exiting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic SecuritySelecting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic Screening => throw new NotImplementedException();

    public async Task<List<RumiPosition>> BackTest(Security security, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1)
    {
        _backTestStartTime = start;
        _freeCash = initialCash;

        var ts = IntervalTypeConverter.ToTimeSpan(intervalType).TotalDays;

        var fastMa = new SimpleMovingAverage(3, "FAST SMA");
        var slowMa = new ExponentialMovingAverage(5, 2, "SLOW EMA");
        var rumiMa = new SimpleMovingAverage(3, "RUMI SMA");
        var prices = await _historicalMarketDataService.Get(security, intervalType, start, end);

        RumiPosition? lastPosition = null;
        var entries = new List<RumiPosition>(prices.Count);
        OhlcPrice? lastOhlcPrice = null;
        foreach (OhlcPrice? ohlcPrice in prices)
        {
            var position = new RumiPosition();
            var price = OhlcPrice.PriceElementSelectors[PriceElementType.Close](ohlcPrice);
            var low = ohlcPrice.L;

            var fast = fastMa.Next(price);
            var slow = slowMa.Next(price);
            var rumi = (fast.IsValid() && slow.IsValid()) ? rumiMa.Next(fast - slow) : decimal.MinValue;

            position.Fast = fast;
            position.Slow = slow;
            position.Rumi = rumi;
            position.Price = price;
            position.FreeCash = _freeCash;

            // copy over most of the states from last to this
            if (lastPosition != null && !lastPosition.IsClosing)
            {
                CopyPosition(position, lastPosition, price);
            }

            // mimic stop loss
            if (position.IsOpened && low <= position.StopLossPrice)
            {
                // assuming always stopped loss at the triggering market stopLossPrice
                StopLoss(position, lastPosition!, GetOhlcEndTime(ohlcPrice, intervalType));
            }

            if (lastPosition != null && lastPosition.StopLossPrice == 0 && lastPosition.IsOpened)
            {
                Console.WriteLine("BUG!");
            }

            // try open or currentPrice a position
            // assuming no margin short-sell is allowed
            if (lastPosition != null && lastPosition.Rumi.IsValid())
            {
                position.IsLongSignal = lastPosition.Rumi < 0 && position.Rumi > 0;
                position.IsShortSignal = lastPosition.Rumi > 0 && position.Rumi < 0;
                if (!position.IsOpened && position.IsLongSignal)
                {
                    // calculate SL and try to round-up
                    OpenPosition(position, lastPosition, price, GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));
                }
                if (position.IsOpened && position.IsShortSignal)
                {
                    ClosePosition(position, price, GetOhlcEndTime(ohlcPrice, intervalType));
                }
            }

            if (_firstPosition == null)
            {
                position.FreeCash = initialCash;
                position.Notional = initialCash;
                _firstPosition = position;
            }
            lastPosition = position;
            lastOhlcPrice = ohlcPrice;
            entries.Add(position);
        }

        if (lastPosition!=null && lastPosition.IsOpened)
        {
            _log.Info("Attempt to close position at the end of back-testing.");
            ClosePosition(lastPosition, lastOhlcPrice?.C ?? 0, GetOhlcEndTime(lastOhlcPrice, intervalType));
        }

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

    private void OpenPosition(RumiPosition entry, RumiPosition previousEntry, decimal enterPrice, DateTime enterTime, decimal stopLossPrice)
    {
        if (previousEntry.FreeCash == 0)
        {
            Console.WriteLine("BUG!");
        }

        entry.IsOpened = true;
        entry.IsClosing = false;
        // TODO position sizing happens here
        entry.Quantity = _freeCash / enterPrice;
        entry.EnterPrice = enterPrice;
        entry.EnterTime = enterTime;
        entry.ExitPrice = 0;
        entry.ExitTime = DateTime.MinValue;
        entry.StopLossPrice = stopLossPrice;
        entry.IsStopLossTriggered = false;
        entry.FreeCash = 0;
        entry.Notional = entry.Quantity * enterPrice; // this does not indicates the free money
        entry.RealizedPnl = 0;
        entry.UnrealizedPnl = 0;
        entry.OrderReturn = 0;
        entry.PortfolioAnnualizedReturn = previousEntry.PortfolioAnnualizedReturn;

        _log.Info($"Opened position: [{entry.EnterTime:yyMMdd-HHmm}] [{entry.EnterPrice:F2}*{entry.Quantity:F2}] SL[{entry.StopLossPrice:F2}]");
        _freeCash = 0;
    }

    private void ClosePosition(RumiPosition current, decimal last, DateTime exitTime)
    {
        if (last == 0 || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        current.IsOpened = false;
        current.IsClosing = true;
        current.ExitPrice = last;
        current.ExitTime = exitTime;
        current.IsStopLossTriggered = false;
        current.RealizedPnl = (last - current.EnterPrice) * current.Quantity;
        current.Notional = current.Quantity * last;
        current.UnrealizedPnl = 0;
        current.OrderReturn = (last - current.EnterPrice) / current.EnterPrice;
        current.PortfolioAnnualizedReturn = Math.Pow(decimal.ToDouble(current.Notional / _firstPosition!.Notional), 365 / (exitTime - _backTestStartTime).TotalDays) - 1;
        current.FreeCash = current.Notional;
        _log.Info($"Closed position: [{current.EnterTime:yyMMdd-HHmm}] [({current.ExitPrice:F2}-{current.EnterPrice:F2})*{current.Quantity:F2}={current.RealizedPnl:F2}][{current.OrderReturn:P2}] SL[{current.StopLossPrice:F2}]");

        if (current.OrderReturn < StopLossRatio * -1)
        {
            Console.WriteLine("BUG!");
        }
        if (current.FreeCash == 0)
        {
            Console.WriteLine("BUG!");
        }

        _freeCash = current.Notional;
    }

    private void StopLoss(RumiPosition current, RumiPosition last, DateTime exitTime)
    {
        var enterPrice = last.EnterPrice;
        var quantity = last.Quantity;
        var sl = last.StopLossPrice;

        current.IsOpened = false;
        current.IsClosing = true;
        current.Quantity = quantity;
        current.EnterPrice = enterPrice;
        current.EnterTime = last.EnterTime;
        current.ExitPrice = sl;
        current.ExitTime = exitTime;
        current.StopLossPrice = sl;
        current.IsStopLossTriggered = true;
        current.UnrealizedPnl = 0;
        current.RealizedPnl = (sl - enterPrice) * quantity;
        current.Notional = last.Notional + current.RealizedPnl;
        current.OrderReturn = (sl - enterPrice) / enterPrice;
        // (Bt/B0)^(1/(Tt-T0))
        current.PortfolioAnnualizedReturn = Math.Pow(decimal.ToDouble(current.Notional / _firstPosition!.Notional), 365 / (exitTime - _backTestStartTime).TotalDays) - 1;
        current.FreeCash = current.Notional;
        _log.Info($"StopLossed: [{current.EnterTime:yyMMdd-HHmm}] [({current.StopLossPrice:F2}-{current.EnterPrice:F2})*{current.Quantity:F2}={current.RealizedPnl:F2}][{current.OrderReturn:P2}] SL[{current.StopLossPrice:F2}]");

        if (current.OrderReturn < StopLossRatio * -1)
        {
            Console.WriteLine("BUG!");
        }

        _freeCash = current.Notional;
    }

    private void CopyPosition(RumiPosition current, RumiPosition last, decimal currentPrice)
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
        current.PortfolioAnnualizedReturn = last.PortfolioAnnualizedReturn;
    }

    public record RumiPosition
    {
        public decimal Fast { get; set; } = decimal.MinValue;
        public decimal Slow { get; set; } = decimal.MinValue;
        public decimal Rumi { get; set; } = decimal.MinValue;
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
        public double PortfolioAnnualizedReturn { get; set; } = 0;
    }
}
