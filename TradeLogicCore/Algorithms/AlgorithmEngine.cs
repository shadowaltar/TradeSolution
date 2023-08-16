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
    private IntervalType _intervalType;
    private TimeSpan _interval;

    public Dictionary<long, AlgoEntry<T>> OpenedEntries { get; } = new();

    public Portfolio Portfolio { get; private set; }

    public List<Security> SecurityPool { get; private set; }

    public IAlgorithm<T> Algorithm { get; private set; }

    public IPositionSizingAlgoLogic<T> Sizing { get; private set; }

    public IEnterPositionAlgoLogic<T> EnterLogic { get; private set; }

    public IExitPositionAlgoLogic<T> ExitLogic { get; private set; }

    public ISecurityScreeningAlgoLogic<T> Screening { get; private set; }

    public IPriceProvider PriceProvider { get; private set; }

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

    public async Task<List<AlgoEntry<T>>> BackTest(List<Security> securityPool, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1000)
    {
        Portfolio = new Portfolio(initialCash);

        SecurityPool = securityPool;
        _backTestStartTime = start;
        _intervalType = intervalType;
        _interval = IntervalTypeConverter.ToTimeSpan(_intervalType);

        var securities = Screening.Pick(securityPool);
        var entries = new List<AlgoEntry<T>>();

        foreach (var security in securities)
        {
            AlgoEntry<T>? lastEntry = null;
            OhlcPrice? lastOhlcPrice = null;
            var sequenceNum = 0;
            var prices = _historicalMarketDataService.GetAsync(security, intervalType, start, end);
            await foreach (OhlcPrice? ohlcPrice in prices)
            {
                var price = ohlcPrice.C;
                var entry = new AlgoEntry<T>
                {
                    Id = sequenceNum,
                    Time = ohlcPrice.T,
                    Variables = Algorithm.CalculateVariables(price, lastEntry)
                };
                entry.Price = price;

                if (lastEntry == null)
                {
                    lastEntry = entry;
                    lastOhlcPrice = ohlcPrice;
                    entry.Portfolio = Portfolio with { };
                    entries.Add(entry);
                    continue;
                }

                entry.Return = (price - lastEntry.Price) / lastEntry.Price;

                // copy over most of the states from exitPrice to this
                if (lastEntry.LongCloseType == CloseType.None && lastEntry.ShortCloseType == CloseType.None)
                {
                    CopyEntry(entry, lastEntry, price);
                    Portfolio.Notional = GetPortfolioNotional();
                }

                CheckLongStopLoss(entry, lastEntry, ohlcPrice, intervalType);
                CheckShortStopLoss(entry, lastEntry, ohlcPrice, intervalType);

                Assertion.ShallNever(entry.SLPrice == 0 && (entry.IsLong || entry.IsShort));

                var toLong = Algorithm.IsOpenLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
                var toCloseLong = Algorithm.IsCloseLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
                entry.LongSignal = toLong ? SignalType.Open : toCloseLong ? SignalType.Close : SignalType.Hold;

                TryOpenLong(entry, lastEntry, security, ohlcPrice, intervalType, ref sequenceNum);
                TryCloseLong(entry, ohlcPrice, intervalType);

                var toShort = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
                var toCloseShort = Algorithm.IsCloseShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
                entry.ShortSignal = toShort ? SignalType.Open : toCloseShort ? SignalType.Close : SignalType.Hold;

                TryOpenShort(entry, lastEntry, security, ohlcPrice, intervalType, ref sequenceNum);
                TryCloseShort(entry, ohlcPrice, intervalType);

                lastEntry = entry;
                lastOhlcPrice = ohlcPrice;

                Portfolio.TotalRealizedPnl += entry.RealizedPnl;

                entry.Portfolio = Portfolio with { };

                Assertion.ShallNever(Portfolio.Notional == 0);
                entries.Add(entry);
            }

            if (lastEntry != null && lastEntry.IsLong)
            {
                _log.Info("Discard any opened entry at the end of back-testing.");
                Portfolio.FreeCash += lastEntry.EnterPrice.Value * lastEntry.Quantity;
                Portfolio.Notional = Portfolio.FreeCash;
            }

            if (lastEntry != null && lastEntry.IsShort)
            {
                _log.Info("Discard any opened entry at the end of back-testing.");
                throw new NotImplementedException();
            }

            Assertion.Shall((Portfolio.InitialFreeCash + Portfolio.TotalRealizedPnl).ApproxEquals(Portfolio.FreeCash));
        }

        return entries;
    }

    private bool CheckLongStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsLong && ohlcPrice.L <= entry.SLPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.StopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            Portfolio.Notional = GetPortfolioNotional();
            Portfolio.FreeCash += entry.Notional;
            OpenedEntries.Remove(entry.Id);

            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private bool CheckShortStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsShort && ohlcPrice.H >= entry.SLPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.StopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            Portfolio.Notional = GetPortfolioNotional();
            Portfolio.FreeCash += entry.Notional;

            OpenedEntries.Remove(entry.Id);

            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private bool TryOpenLong(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType, ref int sequenceNum)
    {
        if (!entry.IsLong && entry.LongCloseType == CloseType.None && entry.LongSignal == SignalType.Open)
        {
            Algorithm.BeforeOpeningLong(entry);

            sequenceNum++;

            EnterLogic.Open(entry, lastEntry, ohlcPrice.C,
                GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));
            entry.Id = sequenceNum;
            Portfolio.FreeCash -= entry.Notional;

            OpenedEntries[entry.Id] = entry;

            Algorithm.AfterLongOpened(entry);
            return true;
        }
        return false;
    }

    private bool TryCloseLong(AlgoEntry<T> entry, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsLong && entry.LongCloseType == CloseType.None && entry.LongSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingLong(entry);

            ExitLogic.Close(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

            Portfolio.Notional = GetPortfolioNotional();
            Portfolio.FreeCash += entry.Notional;

            OpenedEntries.Remove(entry.Id);

            Algorithm.AfterLongClosed(entry);
            return true;
        }
        return false;
    }

    private bool TryOpenShort(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType, ref int sequenceNum)
    {
        if (!entry.IsShort && entry.ShortCloseType == CloseType.None && entry.ShortSignal == SignalType.Open)
        {
            Algorithm.BeforeOpeningShort(entry);

            sequenceNum++;

            EnterLogic.Open(entry, lastEntry, ohlcPrice.C,
                GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));
            entry.Id = sequenceNum;
            Portfolio.FreeCash -= entry.Notional;

            OpenedEntries[entry.Id] = entry;

            Algorithm.AfterShortOpened(entry);
            return true;
        }
        return false;
    }

    private bool TryCloseShort(AlgoEntry<T> entry, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsShort && entry.ShortCloseType == CloseType.None && entry.ShortSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingShort(entry);

            ExitLogic.Close(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

            Portfolio.Notional = GetPortfolioNotional();
            Portfolio.FreeCash += entry.Notional;

            OpenedEntries.Remove(entry.Id);

            Algorithm.AfterShortClosed(entry);
            return true;
        }
        return false;
    }

    private decimal GetPortfolioNotional()
    {
        return Portfolio.FreeCash + OpenedEntries.Values.Sum(p => p.Notional);
    }

    private decimal GetStopLoss(OhlcPrice price, Security security)
    {
        return decimal.Round(price.C * (1 - ExitLogic.StopLossRatio), security.PricePrecision, MidpointRounding.ToPositiveInfinity);
    }

    private DateTime GetOhlcEndTime(OhlcPrice? price, IntervalType intervalType)
    {
        if (price == null)
            return DateTime.MinValue;
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected override void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice)
    {
        current.IsLong = last.IsLong;
        current.IsShort = last.IsShort;
        if (current.IsLong || current.IsShort)
        {
            current.Quantity = last.Quantity;
            current.EnterPrice = last.EnterPrice;
            current.EnterTime = last.EnterTime;
            current.ExitPrice = last.ExitPrice;
            current.Elapsed = last.Elapsed + _interval;
            current.SLPrice = last.SLPrice;
            current.UnrealizedPnl = (currentPrice - current.EnterPrice!.Value) * current.Quantity;

            Assertion.Shall(current.EnterPrice.HasValue);
            Assertion.Shall(last.Quantity != 0);

            OpenedEntries[current.Id] = current;
        }
        else
        {
            current.LongSignal = SignalType.None;
            current.ShortSignal = SignalType.None;
            current.Quantity = 0;
            current.EnterPrice = null;
            current.EnterTime = null;
            current.ExitPrice = null;
            current.Elapsed = null;
            current.SLPrice = null;
            current.UnrealizedPnl = 0;
        }

        if (last.LongSignal == SignalType.Open)
        {
            current.LongSignal = SignalType.None;
        }
        if (last.ShortSignal == SignalType.Open)
        {
            current.ShortSignal = SignalType.None;
        }

        current.RealizedPnl = 0;
        current.LongCloseType = CloseType.None;
        current.ShortCloseType = CloseType.None;
        current.Notional = current.Quantity * currentPrice;
    }
}
