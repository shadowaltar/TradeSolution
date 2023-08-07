using Common;
using log4net;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public class AlgorithmEngine<T> : IAlgorithmEngine<T>, IAlgorithemContext<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private DateTime _backTestStartTime;

    public Dictionary<long, AlgoEntry<T>> OpenedEntries { get; } = new();

    public Portfolio Portfolio { get; private set; }

    public List<Security> SecurityPool { get; private set; }

    public IAlgorithm<T> Algorithm { get; private set; }

    public IPositionSizingAlgoLogic<T> Sizing { get; private set; }

    public IEnterPositionAlgoLogic<T> EnterLogic { get; private set; }

    public IExitPositionAlgoLogic<T> ExitLogic { get; private set; }

    public ISecurityScreeningAlgoLogic<T> Screening { get; private set; }

    public AlgorithmEngine(IHistoricalMarketDataService historicalMarketDataService)
    {
        _historicalMarketDataService = historicalMarketDataService;
    }

    public void SetAlgorithm(IAlgorithm<T> algorithm,
        IPositionSizingAlgoLogic<T> sizingLogic,
        IEnterPositionAlgoLogic<T> enterLogic,
        IExitPositionAlgoLogic<T> exitLogic,
        ISecurityScreeningAlgoLogic<T> screeningLogic)
    {
        Algorithm = algorithm;
        Sizing = sizingLogic;
        EnterLogic = enterLogic;
        ExitLogic = exitLogic;
        Screening = screeningLogic;
    }

    public void ListenTo(List<Security> securityPool, IntervalType intervalType)
    {
        // TODO reactive to real-time market prices
    }

    public async Task<List<AlgoEntry<T>>> BackTest(List<Security> securityPool, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1)
    {
        Portfolio = new Portfolio(initialCash);

        SecurityPool = securityPool;
        _backTestStartTime = start;

        var ts = IntervalTypeConverter.ToTimeSpan(intervalType).TotalDays;

        var securities = Screening.Pick(securityPool);
        var entries = new List<AlgoEntry<T>>();

        foreach (var security in securities)
        {
            AlgoEntry<T>? lastEntry = null;
            OhlcPrice? lastOhlcPrice = null;
            var openCount = 0;
            var prices = await _historicalMarketDataService.Get(security, intervalType, start, end);
            foreach (OhlcPrice? ohlcPrice in prices)
            {
                var entry = new AlgoEntry<T> { Id = openCount, Time = ohlcPrice.T };

                var price = OhlcPrice.PriceElementSelectors[PriceElementType.Close](ohlcPrice);
                var low = ohlcPrice.L;

                var algorithmVariables = Algorithm.CalculateVariables(price, lastEntry);
                entry.Variables = algorithmVariables;
                entry.Price = price;
                entry.Low = low;
                if (lastEntry == null)
                {
                    lastEntry = entry;
                    lastOhlcPrice = ohlcPrice;
                    entry.Portfolio = Portfolio with { };
                    FirstEntry = entry;
                    entries.Add(entry);
                    continue;
                }

                entry.Return = (price - lastEntry.Price) / lastEntry.Price;

                // copy over most of the states from exitPrice to this
                if (!lastEntry.IsClosing)
                {
                    CopyEntry(entry, lastEntry, price);
                    Portfolio.Notional = GetPortfolioNotional();
                }

                // mimic stop loss
                if (entry.IsOpened && low <= entry.StopLossPrice)
                {
                    // assuming always stopped loss at the stopLossPrice
                    ExitLogic.StopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));

                    Portfolio.Notional = GetPortfolioNotional();
                    Portfolio.FreeCash += entry.Notional;

                    OpenedEntries.Remove(entry.Id);
                }

                Assertion.ShallNever(lastEntry.StopLossPrice == 0 && lastEntry.IsOpened);

                // try open or currentPrice a current
                // assuming no margin short-sell is allowed
                entry.IsLongSignal = Algorithm.IsLongSignal(entry, lastEntry, ohlcPrice);
                entry.IsShortSignal = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice);
                if (!entry.IsOpened && !entry.IsStopLossTriggered && entry.IsLongSignal)
                {
                    openCount++;
                    entry.Id = openCount;

                    // calculate SL and try to round-up
                    EnterLogic.Open(entry, lastEntry, price, GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));

                    Portfolio.FreeCash -= entry.Notional;

                    OpenedEntries[entry.Id] = entry;
                }
                if (entry.IsOpened && entry.IsShortSignal)
                {
                    ExitLogic.Close(entry, price, GetOhlcEndTime(ohlcPrice, intervalType));

                    Portfolio.Notional = GetPortfolioNotional();
                    Portfolio.FreeCash += entry.Notional;

                    OpenedEntries.Remove(entry.Id);
                }

                lastEntry = entry;
                lastOhlcPrice = ohlcPrice;

                Portfolio.TotalRealizedPnl += entry.RealizedPnl;

                entry.Portfolio = Portfolio with { };

                Assertion.ShallNever(Portfolio.Notional == 0);
                entries.Add(entry);
            }

            if (lastEntry != null && lastEntry.IsOpened)
            {
                _log.Info("Discard any opened entry at the end of back-testing.");
                Portfolio.FreeCash += lastEntry.EnterPrice * lastEntry.Quantity;
                Portfolio.Notional = Portfolio.FreeCash;
            }

            Assertion.Shall((Portfolio.InitialFreeCash + Portfolio.TotalRealizedPnl).ApproxEquals(Portfolio.FreeCash));
        }

        return entries;
    }

    private decimal GetPortfolioNotional()
    {
        return Portfolio.FreeCash + OpenedEntries.Values.Sum(p => p.Notional);
    }

    private decimal GetStopLoss(OhlcPrice price, Security security)
    {
        return decimal.Round(price.C * (1 - ExitLogic.StopLossRatio), security.PriceDecimalPoints, MidpointRounding.ToPositiveInfinity);
    }

    private DateTime GetOhlcEndTime(OhlcPrice? price, IntervalType intervalType)
    {
        if (price == null)
            return DateTime.MinValue;
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected override void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice)
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
            current.UnrealizedPnl = (currentPrice - current.EnterPrice) * current.Quantity;
            current.RealizedPnl = 0;

            OpenedEntries[current.Id] = current;
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
            current.UnrealizedPnl = 0;
            current.RealizedPnl = 0;
        }
        current.Notional = current.Quantity * currentPrice;
    }
}
