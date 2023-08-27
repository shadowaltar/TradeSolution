using Common;
using log4net;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.MarketData;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;
public class AlgorithmEngine<T> : IAlgorithmEngine<T>, IAlgorithmContext<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly IServices _services;
    private readonly int _engineThreadId;
    private IntervalType _intervalType;
    private TimeSpan _interval;

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is security id.
    /// </summary>
    private Dictionary<int, AlgoEntry<T>?> _lastEntryBySecurityIds = new();

    /// <summary>
    /// Caches full history of entries
    /// </summary>
    private Dictionary<int, List<AlgoEntry<T>>> _entriesBySecurityIds = new();

    /// <summary>
    /// </summary>
    private readonly Dictionary<long, AlgoEntry<T>> _openedEntries;

    private OhlcPrice? _lastOhlcPrice = null;

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action ReachedDesignatedEndTime;

    public IPositionSizingAlgoLogic<T> Sizing { get; protected set; }
    public IEnterPositionAlgoLogic<T> EnterLogic { get; protected set; }
    public IExitPositionAlgoLogic<T> ExitLogic { get; protected set; }
    public ISecurityScreeningAlgoLogic Screening { get; protected set; }

    public List<Security> SecurityPool { get; private set; }

    public IAlgorithm<T> Algorithm { get; }

    public User? User { get; protected set; }

    public Account? Account { get; protected set; }

    public DateTime? DesignatedHaltTime { get; protected set; }

    public DateTime? DesignatedResumeTime
    {
        get; protected set;
    }

    public DateTime? DesignatedStartTime { get; protected set; }

    public DateTime? DesignatedStopTime { get; protected set; }

    public int? HoursBeforeHalt { get; protected set; }

    public IntervalType Interval { get; protected set; }

    public decimal InitialFreeAmount { get; protected set; }

    public Dictionary<long, AlgoEntry<T>> OpenedEntries { get; protected set; }

    public List<Position> OpenPositions { get; protected set; }

    public Portfolio Portfolio { get; protected set; }

    public bool ShouldCloseOpenPositionsWhenHalted { get; protected set; }

    public bool ShouldCloseOpenPositionsWhenStopped { get; protected set; }

    public AlgoStopTimeType WhenToStopOrHalt { get; protected set; }

    public AlgorithmEngine(IServices services, IAlgorithm<T> algorithm)
    {
        _historicalMarketDataService = services.HistoricalMarketData;
        _services = services;

        _engineThreadId = Environment.CurrentManagedThreadId;

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;
    }
    public void Halt(DateTime? resumeTime, bool isManuallyHalted = false)
    {
        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId == threadId); // we are to halt the main engine thread

        var now = DateTime.UtcNow;
        if (resumeTime != null && resumeTime.Value >= now)
        {
            _runningState = AlgoRunningState.Halted;
            var remainingTimeSpan = (now - resumeTime.Value).Add(TimeSpan.FromMilliseconds(1));
            Thread.Sleep(remainingTimeSpan);
            _runningState = AlgoRunningState.Running;
        }
    }

    public async Task Run(AlgoStartupParameters parameters)
    {
        SetAlgoEffectiveTimeRange(parameters.TimeRange);

        User = await _services.Admin.GetUser(parameters.UserName, parameters.Environment);
        Account = await _services.Admin.GetAccount(parameters.UserName, parameters.Environment);
        if (User == null || Account == null)
            return;
        Interval = parameters.Interval;
        InitialFreeAmount = Account.MainBalance?.FreeAmount ?? 0;
        if (InitialFreeAmount == 0)
            return;

        ShouldCloseOpenPositionsWhenHalted = parameters.ShouldCloseOpenPositionsWhenHalted;
        ShouldCloseOpenPositionsWhenStopped = parameters.ShouldCloseOpenPositionsWhenStopped;

        _services.MarketData.NextOhlc -= OnNextPrice;
        _services.MarketData.NextOhlc += OnNextPrice;

        Screening.SetAndPick(parameters.SecurityPool);
        var pickedSecurities = Screening.GetPickedOnes();
        foreach (var security in pickedSecurities)
        {
            await _services.MarketData.SubscribeOhlc(security, Interval);
        }
    }

    public void ScheduleMaintenance(DateTime haltTime, DateTime resumeTime)
    {
        if (haltTime.IsValid())
            DesignatedHaltTime = haltTime;
        if (resumeTime.IsValid())
            DesignatedResumeTime = resumeTime;
    }

    private void SetAlgoEffectiveTimeRange(AlgoEffectiveTimeRange timeRange)
    {
        DateTime? stopTime = null;
        var now = DateTime.UtcNow;
        var localNow = now.ToLocalTime();

        // handle the stop time
        switch (timeRange.WhenToStop)
        {
            case AlgoStopTimeType.Designated:
                stopTime = timeRange.DesignatedStop;
                if (stopTime != null && stopTime > now)
                    DesignatedStopTime = stopTime;
                break;
            case AlgoStopTimeType.Never:
                DesignatedStopTime = null;
                break;
            case AlgoStopTimeType.BeforeBrokerMaintenance:
                if (timeRange.HoursBeforeMaintenance < 0)
                    throw new ArgumentException("Invalid hours before maintenance.");
                HoursBeforeHalt = timeRange.HoursBeforeMaintenance;
                break;
        }

        // handle (and wait for) the start time
        var startTime = timeRange.ActualStartTime;
        switch (timeRange.WhenToStart)
        {
            case AlgoStartTimeType.Designated:
                {
                    if (startTime.IsValid() && startTime > now)
                    {
                        WaitTillStartTime(startTime, stopTime, now);
                    }
                    else
                    {
                        _log.Error($"Invalid designated algo start time: {startTime:yyyyMMdd-HHmmss}");
                    }
                    break;
                }
            case AlgoStartTimeType.Immediately:
                _runningState = AlgoRunningState.Running;
                break;
            case AlgoStartTimeType.Never:
                _runningState = AlgoRunningState.Stopped;
                break;
            case AlgoStartTimeType.NextStartOf:
                if (timeRange.NextStartOfIntervalType != null)
                {
                    if (startTime > now)
                    {
                        WaitTillStartTime(startTime, stopTime, now);
                    }
                    else
                    {
                        _log.Error($"Invalid designated algo start time: {startTime:yyyyMMdd-HHmmss}");
                    }
                }
                else
                {
                    _log.Error($"Invalid designated algo start time type, missing interval for \"NextStartOf\" type.");
                }
                break;
            case AlgoStartTimeType.NextStartOfLocalDay:
                {
                    _runningState = AlgoRunningState.Stopped;
                    if (startTime > localNow)
                    {
                        if (stopTime != null) stopTime = stopTime.Value.ToLocalTime();
                        WaitTillStartTime(startTime, stopTime, localNow);
                    }
                    else
                    {
                        _log.Error($"Invalid designated local algo start time: {startTime:yyyyMMdd-HHmmss}");
                    }
                    break;
                }
            case AlgoStartTimeType.NextMarketOpens:
                // TODO, need market meta data
                break;
            case AlgoStartTimeType.NextWeekMarketOpens:
                // TODO, need market meta data
                break;
        }

        void WaitTillStartTime(DateTime startTime, DateTime? stopTime, DateTime now)
        {
            if (stopTime != null && startTime > stopTime)
            {
                throw new ArgumentException("Start time is larger than stop time. Program exits.");
            }
            DesignatedStartTime = startTime;
            var remainingTimeSpan = now - startTime;
            _log.Info($"Wait till {startTime:yyyyMMdd-HHmmss}, remaining: {remainingTimeSpan.TotalSeconds:F4} seconds.");
            Halt(startTime);
        }
    }
    public async Task Stop()
    {
        _runningState = AlgoRunningState.Stopped;
        _services.MarketData.NextOhlc -= OnNextPrice;
        await _services.MarketData.UnsubscribeAllOhlcs();
        var securities = Screening.GetAll();
        foreach (var security in securities)
        {
            await _services.MarketData.UnsubscribeOhlc(security, Interval);
        }
    }

    /// <summary>
    /// Handler which is invoked when price feed notifies a new price object arrives.
    /// We expect this is a separated thread from the original engine.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnNextPrice(int securityId, OhlcPrice ohlcPrice)
    {
        ProcessRunningState();
        if (_runningState != AlgoRunningState.Running)
            return;

        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId != threadId);

        var securities = Screening.GetPickedOnes();
        foreach (var security in securities)
        {
            Algorithm.BeforeProcessingSecurity(this, security);
            var entries = _entriesBySecurityIds.GetOrCreate(security.Id);
            var lastEntry = _lastEntryBySecurityIds.GetValueOrDefault(security.Id);
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

    private void ProcessRunningState()
    {
        // check if it is time to halt or stop, due to market close or suspension, or end of simulation
        var now = DateTime.UtcNow;
        // stop state always has higher precedence
        if (DesignatedStartTime <= now)
        {
            _runningState = AlgoRunningState.Running;
        }
        else
        {
            _runningState = AlgoRunningState.NotYetStarted;
        }
        if (DesignatedResumeTime <= now)
        {
            _runningState = AlgoRunningState.Running;
        }
        if (DesignatedHaltTime <= now && DesignatedResumeTime > now)
        {
            _runningState = AlgoRunningState.Halted;
        }
        if (DesignatedStopTime <= now && DesignatedStartTime > now)
        {
            _runningState = AlgoRunningState.Stopped;
        }

        if (_runningState == AlgoRunningState.Stopped && ShouldCloseOpenPositionsWhenStopped && OpenPositions.Count != 0)
        {
            CloseAllOpenPositions();
        }
        else if (_runningState == AlgoRunningState.Halted && ShouldCloseOpenPositionsWhenHalted && OpenPositions.Count != 0)
        {
            CloseAllOpenPositions();
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
            _openedEntries.Remove(entry.Id);

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

            _openedEntries.Remove(entry.Id);

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

            _openedEntries[entry.Id] = entry;

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

            _openedEntries.Remove(entry.Id);

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

            _openedEntries[entry.Id] = entry;

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

            _openedEntries.Remove(entry.Id);

            Algorithm.AfterShortClosed(entry);
            return true;
        }
        return false;
    }

    private decimal GetPortfolioNotional()
    {
        return Portfolio.FreeCash + _openedEntries.Values.Sum(p => p.Notional);
    }

    private decimal GetStopLoss(OhlcPrice price, Security security)
    {
        return decimal.Round(price.C * (1 - ExitLogic.StopLossRatio), security.PricePrecision, MidpointRounding.ToPositiveInfinity);
    }

    private DateTime GetOhlcEndTime(OhlcPrice? price, IntervalType intervalType)
    {
        return price == null ? DateTime.MinValue : price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, decimal currentPrice)
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

            _openedEntries[current.Id] = current;
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

    private void CloseAllOpenPositions()
    {
        _services.Order.CancelAllOpenOrders();
        _services.Order.CloseAllOpenPositions();
    }
}

public enum AlgoRunningState
{
    NotYetStarted,
    Running,
    Halted,
    Stopped
}