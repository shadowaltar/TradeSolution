using Common;
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
    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    private RumiEntry? _firstEntry;
    private DateTime _backTestStartTime;

    public decimal StopLossRatio { get; set; } = 0;

    public Rumi(IHistoricalMarketDataService historicalMarketDataService)
    {
        _historicalMarketDataService = historicalMarketDataService;
    }

    public IPositionSizingAlgoLogic Sizing => throw new NotImplementedException();

    public IEnterPositionAlgoLogic Entering => throw new NotImplementedException();

    public IExitPositionAlgoLogic Exiting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic SecuritySelecting => throw new NotImplementedException();

    public ISecuritySelectionAlgoLogic Screening => throw new NotImplementedException();

    public async Task<List<RumiEntry>> BackTest(Security security, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1)
    {
        _backTestStartTime = start;

        var ts = IntervalTypeConverter.ToTimeSpan(intervalType).TotalDays;

        var fastMa = new SimpleMovingAverage(3, "FAST SMA");
        var slowMa = new ExponentialMovingAverage(5, 2, "SLOW EMA");
        var rumiMa = new SimpleMovingAverage(3, "RUMI SMA");
        var prices = await _historicalMarketDataService.Get(security, intervalType, start, end);

        RumiEntry? previousEntry = null;
        var entries = new List<RumiEntry>(prices.Count);
        foreach (OhlcPrice? price in prices)
        {
            var entry = new RumiEntry();
            var close = OhlcPrice.PriceElementSelectors[PriceElementType.Close](price);

            var fast = fastMa.Next(close);
            var slow = slowMa.Next(close);
            var rumi = (fast.IsValid() && slow.IsValid()) ? rumiMa.Next(fast - slow) : decimal.MinValue;

            entry.Fast = fast;
            entry.Slow = slow;
            entry.Rumi = rumi;
            entry.Price = close;

            entries.Add(entry);

            // mimic stop loss
            if (previousEntry != null && price.L <= previousEntry.StopLossPrice)
            {
                // assuming always stopped loss at the triggering market stopLossPrice
                StopLoss(entry, previousEntry, GetOhlcEndTime(price, intervalType));
            }
            //if (previousEntry != null)
            //{
            //    HoldPosition(entry, previousEntry, close);
            //}
            // try open or currentPrice a position
            // assuming no margin short-sell is allowed
            var isJustOpened = false;
            var isJustClosed = false;
            if (previousEntry != null && previousEntry.Rumi.IsValid())
            {
                entry.IsLongSignal = previousEntry.Rumi < 0 && entry.Rumi > 0;
                entry.IsShortSignal = previousEntry.Rumi > 0 && entry.Rumi < 0;
                if (!previousEntry.HasPosition && entry.IsLongSignal)
                {
                    // calculate SL and try to round-up
                    var stopLossPrice = decimal.Round(close * (1 - StopLossRatio), security.PriceDecimalPoints, MidpointRounding.ToPositiveInfinity);
                    OpenPosition(entry, previousEntry, close, GetOhlcEndTime(price, intervalType), stopLossPrice);
                    isJustOpened = true;
                }
                if (previousEntry.HasPosition && entry.IsShortSignal)
                {
                    ClosePosition(entry, previousEntry, close, GetOhlcEndTime(price, intervalType));
                    isJustClosed = true;
                }
            }
            //if (previousEntry != null && !isJustOpened && !isJustClosed)
            //{
            //    HoldPosition(entry, previousEntry, close);
            //}

            var firstEntry = previousEntry == null;
            previousEntry = entry;
            if (firstEntry)
            {
                previousEntry.FreeCash = initialCash;
                previousEntry.Notional = initialCash;
                _firstEntry = entry;
            }
        }
        return entries;

        DateTime GetOhlcEndTime(OhlcPrice price, IntervalType intervalType)
        {
            return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
        }
    }

    private void OpenPosition(RumiEntry entry, RumiEntry previousEntry, decimal enterPrice, DateTime tradeTime, decimal stopLossPrice)
    {
        entry.HasPosition = true;
        entry.Quantity = previousEntry.FreeCash / enterPrice;
        entry.EnterPrice = enterPrice;
        entry.EnterTime = tradeTime;
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
    }

    private void ClosePosition(RumiEntry entry, RumiEntry previousEntry, decimal exitPrice, DateTime exitTime)
    {
        var quantity = previousEntry.Quantity;
        var enterPrice = previousEntry.EnterPrice;

        entry.HasPosition = false;
        entry.Quantity = quantity;
        entry.EnterPrice = enterPrice;
        entry.EnterTime = previousEntry.EnterTime;
        entry.ExitPrice = exitPrice;
        entry.ExitTime = exitTime;
        entry.StopLossPrice = 0;
        entry.IsStopLossTriggered = false;
        entry.RealizedPnl = (exitPrice - enterPrice) * quantity;
        entry.Notional = quantity * exitPrice;
        entry.UnrealizedPnl = 0;
        entry.OrderReturn = (exitPrice - enterPrice) / enterPrice;

        if (entry.OrderReturn < StopLossRatio * -1)
        {

        }


        entry.PortfolioAnnualizedReturn = Math.Pow(decimal.ToDouble(entry.Notional / _firstEntry!.Notional), 365 / (exitTime - _backTestStartTime).TotalDays) - 1;
        entry.FreeCash = entry.Notional;
    }

    private void StopLoss(RumiEntry entry, RumiEntry previousEntry, DateTime exitTime)
    {
        var enterPrice = previousEntry.EnterPrice;
        var quantity = previousEntry.Quantity;
        var sl = previousEntry.StopLossPrice;

        entry.HasPosition = false;
        entry.Quantity = 0;
        entry.EnterPrice = enterPrice;
        entry.EnterTime = previousEntry.EnterTime;
        entry.ExitPrice = sl;
        entry.ExitTime = exitTime;
        entry.StopLossPrice = sl;
        entry.IsStopLossTriggered = true;
        entry.UnrealizedPnl = 0;
        entry.RealizedPnl = (sl - enterPrice) * quantity;
        entry.Notional = previousEntry.Notional + entry.RealizedPnl;
        entry.OrderReturn = (sl - enterPrice) / enterPrice;

        if (entry.OrderReturn < StopLossRatio * -1)
        {

        }

        // (Bt/B0)^(1/(Tt-T0))
        entry.PortfolioAnnualizedReturn = Math.Pow(decimal.ToDouble(entry.Notional / _firstEntry!.Notional), 365 / (exitTime - _backTestStartTime).TotalDays) - 1;
        entry.FreeCash = entry.Notional;
    }

    private void HoldPosition(RumiEntry entry, RumiEntry previousEntry, decimal currentPrice)
    {
        entry.HasPosition = previousEntry.HasPosition;
        if (entry.HasPosition)
        {
            entry.Quantity = previousEntry.Quantity;
            entry.EnterPrice = previousEntry.EnterPrice;
            entry.EnterTime = previousEntry.EnterTime;
            entry.ExitPrice = previousEntry.ExitPrice;
            entry.ExitTime = previousEntry.ExitTime;
            entry.StopLossPrice = previousEntry.StopLossPrice;
            entry.IsStopLossTriggered = false;
            entry.FreeCash = previousEntry.FreeCash;
            entry.UnrealizedPnl = (currentPrice - entry.EnterPrice) * entry.Quantity;
            entry.RealizedPnl = 0;
            entry.Notional = entry.Quantity * entry.EnterPrice + entry.UnrealizedPnl;
        }
        else
        {
            entry.Quantity = 0;
            entry.EnterPrice = 0;
            entry.EnterTime = DateTime.MinValue;
            entry.ExitPrice = 0;
            entry.ExitTime = DateTime.MinValue;
            entry.StopLossPrice = 0;
            entry.IsStopLossTriggered = false;
            entry.FreeCash = previousEntry.FreeCash;
            entry.UnrealizedPnl = 0;
            entry.RealizedPnl = 0;
            entry.Notional = entry.FreeCash;
        }
        entry.PortfolioAnnualizedReturn = previousEntry.PortfolioAnnualizedReturn;
    }

    public record RumiEntry
    {
        public decimal Fast { get; set; } = decimal.MinValue;
        public decimal Slow { get; set; } = decimal.MinValue;
        public decimal Rumi { get; set; } = decimal.MinValue;
        public decimal Price { get; set; } = decimal.MinValue;

        public bool IsLongSignal { get; set; }
        public bool IsShortSignal { get; set; }
        public bool HasPosition { get; set; }

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
