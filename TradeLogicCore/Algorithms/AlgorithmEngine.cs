using Common;
using log4net;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;
public class AlgorithmEngine<T> : IAlgorithmEngine<T>, IAlgorithmContext<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly IServices _services;
    private IntervalType _intervalType;
    private TimeSpan _interval;

    public Dictionary<long, AlgoEntry<T>> OpenedEntries { get; } = new();

    public Portfolio Portfolio { get; private set; }

    public List<Security> SecurityPool { get; private set; }

    public IAlgorithm<T> Algorithm { get; private set; }

    public IPriceProvider PriceProvider { get; private set; }

    public AlgorithmEngine(IServices services, IAlgorithm<T> algorithm)
    {
        _historicalMarketDataService = services.HistoricalMarketData;
        _services = services;

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;
    }

    public void Run(List<Security> securityPool, IntervalType intervalType, Parameters.AlgoEffectiveTimeRange effectiveTimeRange)
    {
        Screening.SetAndPick(securityPool);
        var pickedSecurities = Screening.GetPickedOnes();
        foreach (var security in pickedSecurities)
        {
            _services.RealTimeMarketData.SubscribeOhlc(security);
        }
    }

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is security id.
    /// </summary>
    private Dictionary<int, AlgoEntry<T>?> _lastEntries = new();

    private OhlcPrice? _lastOhlcPrice = null;

    /// <summary>
    /// Caches full history of entries
    /// </summary>
    private Dictionary<int, List<AlgoEntry<T>>> _entries = new();

    private void OnNextPrice(int securityId, OhlcPrice ohlcPrice)
    {
        var securities = Screening.GetPickedOnes();
        foreach (var security in securities)
        {
            Algorithm.BeforeProcessingSecurity(this, security);
            var entries = _entries.GetOrCreate(security.Id);
            AlgoEntry<T>? lastEntry = _lastEntries.GetValueOrDefault(security.Id);
            var sequenceNum = 0;
            var price = ohlcPrice.C;
            var entry = new AlgoEntry<T>
            {
                Id = sequenceNum,
                Time = ohlcPrice.T,
                Variables = Algorithm.CalculateVariables(price, lastEntry),
                Price = price
            };

            if (lastEntry == null)
            {
                lastEntry = entry;
                _lastOhlcPrice = ohlcPrice;
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

            CheckLongStopLoss(entry, lastEntry, ohlcPrice, _intervalType);
            CheckShortStopLoss(entry, lastEntry, ohlcPrice, _intervalType);

            Assertion.ShallNever(entry.SLPrice == 0 && (entry.IsLong || entry.IsShort));

            var toLong = Algorithm.IsOpenLongSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
            var toCloseLong = Algorithm.IsCloseLongSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
            entry.LongSignal = toLong ? SignalType.Open : toCloseLong ? SignalType.Close : SignalType.Hold;

            TryOpenLong(entry, lastEntry, security, ohlcPrice, _intervalType, ref sequenceNum);
            TryCloseLong(entry, ohlcPrice, _intervalType);

            var toShort = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
            var toCloseShort = Algorithm.IsCloseShortSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
            entry.ShortSignal = toShort ? SignalType.Open : toCloseShort ? SignalType.Close : SignalType.Hold;

            TryOpenShort(entry, lastEntry, security, ohlcPrice, _intervalType, ref sequenceNum);
            TryCloseShort(entry, ohlcPrice, _intervalType);

            lastEntry = entry;
            _lastOhlcPrice = ohlcPrice;

            Portfolio.TotalRealizedPnl += entry.RealizedPnl;

            entry.Portfolio = Portfolio with { };

            Assertion.ShallNever(Portfolio.Notional == 0);
            entries.Add(entry);

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

            //Assertion.Shall((Portfolio.InitialFreeCash + Portfolio.TotalRealizedPnl).ApproxEquals(Portfolio.FreeCash));

            Algorithm.AfterProcessingSecurity(this, security);
        }
    }

    //public async Task<List<AlgoEntry<T>>> BackTest(List<Security> securityPool, IntervalType intervalType, DateTime start, DateTime end, decimal initialCash = 1000)
    //{
    //    Algorithm.BeforeAlgoExecution(this);
    //    Portfolio = new Portfolio(initialCash);

    //    SecurityPool = securityPool;
    //    _intervalType = intervalType;
    //    _interval = IntervalTypeConverter.ToTimeSpan(_intervalType);

    //    _services.RealTimeMarketData.NextOhlc -= OnNextPrice;
    //    _services.RealTimeMarketData.NextOhlc += OnNextPrice;

    //    foreach (var security in SecurityPool)
    //    {
    //        await _services.RealTimeMarketData.SubscribeOhlc(security);
    //    }
    //    var entries = new List<AlgoEntry<T>>();

    //    foreach (var security in securities)
    //    {
    //        Algorithm.BeforeProcessingSecurity(this, security);

    //        AlgoEntry<T>? lastEntry = null;
    //        OhlcPrice? lastOhlcPrice = null;
    //        var sequenceNum = 0;
    //        var prices = _historicalMarketDataService.GetAsync(security, intervalType, start, end);
    //        await foreach (OhlcPrice? ohlcPrice in prices)
    //        {
    //            var price = ohlcPrice.C;
    //            var entry = new AlgoEntry<T>
    //            {
    //                Id = sequenceNum,
    //                Time = ohlcPrice.T,
    //                Variables = Algorithm.CalculateVariables(price, lastEntry),
    //                Price = price
    //            };

    //            if (lastEntry == null)
    //            {
    //                lastEntry = entry;
    //                lastOhlcPrice = ohlcPrice;
    //                entry.Portfolio = Portfolio with { };
    //                entries.Add(entry);
    //                continue;
    //            }

    //            entry.Return = (price - lastEntry.Price) / lastEntry.Price;

    //            // copy over most of the states from exitPrice to this
    //            if (lastEntry.LongCloseType == CloseType.None && lastEntry.ShortCloseType == CloseType.None)
    //            {
    //                CopyEntry(entry, lastEntry, price);
    //                Portfolio.Notional = GetPortfolioNotional();
    //            }

    //            CheckLongStopLoss(entry, lastEntry, ohlcPrice, intervalType);
    //            CheckShortStopLoss(entry, lastEntry, ohlcPrice, intervalType);

    //            Assertion.ShallNever(entry.SLPrice == 0 && (entry.IsLong || entry.IsShort));

    //            var toLong = Algorithm.IsOpenLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
    //            var toCloseLong = Algorithm.IsCloseLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
    //            entry.LongSignal = toLong ? SignalType.Open : toCloseLong ? SignalType.Close : SignalType.Hold;

    //            TryOpenLong(entry, lastEntry, security, ohlcPrice, intervalType, ref sequenceNum);
    //            TryCloseLong(entry, ohlcPrice, intervalType);

    //            var toShort = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
    //            var toCloseShort = Algorithm.IsCloseShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
    //            entry.ShortSignal = toShort ? SignalType.Open : toCloseShort ? SignalType.Close : SignalType.Hold;

    //            TryOpenShort(entry, lastEntry, security, ohlcPrice, intervalType, ref sequenceNum);
    //            TryCloseShort(entry, ohlcPrice, intervalType);

    //            lastEntry = entry;
    //            lastOhlcPrice = ohlcPrice;

    //            Portfolio.TotalRealizedPnl += entry.RealizedPnl;

    //            entry.Portfolio = Portfolio with { };

    //            Assertion.ShallNever(Portfolio.Notional == 0);
    //            entries.Add(entry);
    //        }

    //        if (lastEntry != null && lastEntry.IsLong)
    //        {
    //            _log.Info("Discard any opened entry at the end of back-testing.");
    //            Portfolio.FreeCash += lastEntry.EnterPrice.Value * lastEntry.Quantity;
    //            Portfolio.Notional = Portfolio.FreeCash;
    //        }

    //        if (lastEntry != null && lastEntry.IsShort)
    //        {
    //            _log.Info("Discard any opened entry at the end of back-testing.");
    //            throw new NotImplementedException();
    //        }

    //        //Assertion.Shall((Portfolio.InitialFreeCash + Portfolio.TotalRealizedPnl).ApproxEquals(Portfolio.FreeCash));

    //        Algorithm.AfterProcessingSecurity(this, security);
    //    }

    //    Algorithm.AfterAlgoExecution(this);
    //    return entries;
    //}

    private bool CheckLongStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsLong && ohlcPrice.L <= entry.SLPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.StopLoss(this, entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
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
            ExitLogic.StopLoss(this, entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
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
            var endTimeOfBar = GetOhlcEndTime(ohlcPrice, intervalType);
            var sl = GetStopLoss(ohlcPrice, security);
            EnterLogic.Open(this, entry, lastEntry, ohlcPrice.C, endTimeOfBar, sl);
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

            ExitLogic.Close(this, entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

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

            EnterLogic.Open(this, entry, lastEntry, ohlcPrice.C,
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

            ExitLogic.Close(this, entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

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
        return price == null ? DateTime.MinValue : price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
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
            current.Fee = last.Fee;

            Assertion.Shall(current.EnterPrice.HasValue);
            Assertion.Shall(current.SLPrice.HasValue);
            Assertion.Shall(last.Quantity != 0);
            Assertion.Shall(current.Fee >= 0);

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
            current.Fee = 0;
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

public enum AlgoStartTimeType
{
    Never,
    Immediately,
    Designated,
    NextStartOfMinute,
    NextStartOfHour,
    NextStartOfLocalDay,
    NextStartOfUtcDay,
    NextMarketOpens,
    NextWeekMarketOpens,
    NextStartOfMonth,
}

public enum AlgoStopTimeType
{
    Never,
    Designated,
    BeforeBrokerMaintenance,
}