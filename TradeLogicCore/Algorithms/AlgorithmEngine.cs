using Common;
using log4net;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;
public class AlgorithmEngine<T> : IAlgorithmEngine<T>, IAlgorithmContext<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    private readonly AutoResetEvent _signal = new(false);

    private readonly int _engineThreadId;
    private readonly IntervalType _intervalType;
    private readonly TimeSpan _interval;
    private readonly IdGenerator _idGenerator;

    private IReadOnlyDictionary<int, Security> _pickedSecurities;

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is security id.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry<T>?> _lastEntryBySecurityIds = new();

    /// <summary>
    /// Caches full history of entries.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry<T>>> _allEntriesBySecurityIds = new();

    /// <summary>
    /// Caches entries related to execution only.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry<T>>> _executionEntriesBySecurityIds = new();

    /// <summary>
    /// Caches entries which open positions. Will be removed when a position is closed.
    /// </summary>
    private readonly Dictionary<int, Dictionary<long, AlgoEntry<T>>> _openedEntriesBySecurityIds = new();

    private int _totalCurrentOpenPositions = 0;

    private OhlcPrice? _lastOhlcPrice = null;

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action ReachedDesignatedEndTime;

    public IServices Services { get; }

    public bool IsBackTesting { get; private set; } = true;

    public IPositionSizingAlgoLogic<T> Sizing { get; protected set; }
    public IEnterPositionAlgoLogic<T> EnterLogic { get; protected set; }
    public IExitPositionAlgoLogic<T> ExitLogic { get; protected set; }
    public ISecurityScreeningAlgoLogic<T> Screening { get; protected set; }

    public int TotalSignalCount { get; protected set; }

    public List<Security> SecurityPool { get; private set; }

    public IAlgorithm<T> Algorithm { get; }

    public User? User { get; protected set; }

    public Account? Account { get; protected set; }

    public AlgoStartupParameters Parameters { get; private set; }

    public DateTime? DesignatedHaltTime { get; protected set; }

    public DateTime? DesignatedResumeTime { get; protected set; }

    public DateTime? DesignatedStartTime { get; protected set; }

    public DateTime? DesignatedStopTime { get; protected set; }

    public int? HoursBeforeHalt { get; protected set; }

    public IntervalType Interval { get; protected set; }

    public bool ShouldCloseOpenPositionsWhenHalted { get; protected set; }

    public bool ShouldCloseOpenPositionsWhenStopped { get; protected set; }

    public AlgoStopTimeType WhenToStopOrHalt { get; protected set; }

    public Dictionary<long, AlgoEntry<T>> OpenedEntries => throw new NotImplementedException();

    public AlgorithmEngine(IServices services, IAlgorithm<T> algorithm)
    {
        Services = services;

        _engineThreadId = Environment.CurrentManagedThreadId;

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;

        _idGenerator = new IdGenerator("AlgoEntryIdGen");
    }

    public List<AlgoEntry<T>> GetAllEntries(int securityId)
    {
        return _allEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public List<AlgoEntry<T>> GetExecutionEntries(int securityId)
    {
        return _executionEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public Dictionary<long, AlgoEntry<T>> GetOpenEntries(int securityId)
    {
        return _openedEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public void Halt(DateTime? resumeTime, bool isManuallyHalted = false)
    {
        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId == threadId); // we are to halt the main engine thread

        var now = DateTime.UtcNow;
        if (resumeTime != null && resumeTime.Value >= now)
        {
            _runningState = AlgoRunningState.Halted;
            var remainingTimeSpan = (resumeTime.Value - now).Add(TimeSpans.OneMillisecond);
            Thread.Sleep(remainingTimeSpan);
            _runningState = AlgoRunningState.Running;
        }
    }

    public async Task<int> Run(AlgoStartupParameters parameters)
    {
        TotalSignalCount = 0;
        User = Services.Admin.CurrentUser;
        Account = Services.Admin.CurrentAccount;
        Parameters = parameters;
        if (User == null || Account == null)
            return 0;
        IsBackTesting = parameters.IsBackTesting;
        Interval = parameters.Interval;
        Parameters = parameters;

        // initialize portfolio, and connect to external
        await Services.Portfolio.Initialize();

        //InitialFreeAmounts.AddRange(Account.Balances, b => b.AssetId, b => b.FreeAmount);
        //if (InitialFreeAmounts.All(t => t.Value == 0))
        //    return 0;

        ShouldCloseOpenPositionsWhenHalted = parameters.ShouldCloseOpenPositionsWhenHalted;
        ShouldCloseOpenPositionsWhenStopped = parameters.ShouldCloseOpenPositionsWhenStopped;

        Services.MarketData.NextOhlc -= OnNextPrice;
        Services.MarketData.NextOhlc += OnNextPrice;
        Services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        Services.MarketData.HistoricalPriceEnd += OnHistoricalPriceEnd;
        Services.Order.NextOrder -= OnNextOrder;
        Services.Order.NextOrder += OnNextOrder;
        Services.Trade.NextTrade -= OnNextTrade;
        Services.Trade.NextTrade += OnNextTrade;

        // pick security to trade
        // check associated asset's position, and map any existing position into algo entry
        // then register market quote feed if all good
        Screening.SetAndPick(parameters.SecurityPool.ToDictionary(s => s.Id, s => s));
        var subscriptionCount = 0;
        var pickedSecurities = Screening.GetAll();
        foreach (var security in pickedSecurities.Values)
        {
            var currencyAsset = security.EnsureCurrencyAsset();
            var assetPosition = Services.Portfolio.GetAsset(currencyAsset.Id);
            if (assetPosition == null || assetPosition.Quantity <= 0)
            {
                _log.Warn($"Cannot trade the picked security {security.Code}; the account may not have enough free asset to trade.");
                continue;
            }
            var position = Services.Portfolio.GetPosition(security.Id);
            if (position != null)
            {
                // TODO simplified the logic such that if any open position, just close it
                Services.Order.CreateCloseOrderAndSend(position, OrderType.Market, 0, TimeInForceType.GoodTillCancel);
            }

            await Services.MarketData.SubscribeOhlc(security, Interval);
            subscriptionCount++;
        }
        SetAlgoEffectiveTimeRange(parameters.TimeRange);

        // wait for the price thread to be stopped by unsubscription or forceful algo exit
        _signal.WaitOne();
        _log.Info("Algorithm Engine execution ends, processed " + TotalSignalCount);
        return TotalSignalCount;
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
                stopTime = null;
                DesignatedStopTime = stopTime;
                break;
            case AlgoStopTimeType.BeforeBrokerMaintenance:
                if (timeRange.HoursBeforeMaintenance < 0)
                    throw new ArgumentException("Invalid hours before maintenance.");
                HoursBeforeHalt = timeRange.HoursBeforeMaintenance;
                break;
        }


        //void WaitTillStartTime(DateTime startTime, DateTime? stopTime, DateTime now)
        //{
        //    if (stopTime != null && startTime > stopTime)
        //    {
        //        throw new ArgumentException("Start time is larger than stop time. Program exits.");
        //    }
        //    DesignatedStartTime = startTime;
        //    var remainingTimeSpan = startTime - now;
        //    _log.Info($"Wait till {startTime:yyyyMMdd-HHmmss}, remaining: {remainingTimeSpan.TotalSeconds:F4} seconds.");
        //    Halt(startTime);
        //}
    }

    public async Task Stop()
    {
        _log.Info("Algorithm Engine is shutting down.");

        _runningState = AlgoRunningState.Stopped;
        Services.MarketData.NextOhlc -= OnNextPrice;
        Services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        await Services.MarketData.UnsubscribeAllOhlcs();
        var securities = Screening.GetAll();
        foreach (var security in securities.Values)
        {
            await Services.MarketData.UnsubscribeOhlc(security, Interval);
        }
        // unblock the main thread and let it finish
        _signal.Set();
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
        ClosePositionIfNeeded();

        TotalSignalCount++;
        if (!CanAcceptPrice())
            return;

        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId != threadId);

        if (Screening.TryCheckIfChanged(out var securities) || _pickedSecurities == null)
        {
            _pickedSecurities = securities;
        }
        if (!_pickedSecurities.TryGetValue(securityId, out var security))
        {
            return;
        }

        Algorithm.BeforeProcessingSecurity(this, security);

        var entries = _allEntriesBySecurityIds.GetOrCreate(security.Id);
        var lastEntry = _lastEntryBySecurityIds.GetValueOrDefault(security.Id);
        var price = ohlcPrice.C;

        var entryPositionId = lastEntry?.PositionId ?? 0;
        var entry = new AlgoEntry<T>(_idGenerator.NewTimeBasedId, security)
        {
            SecurityId = securityId,
            PositionId = entryPositionId,
            Time = ohlcPrice.T,
            Variables = Algorithm.CalculateVariables(price, lastEntry),
            Price = price
        };

        if (lastEntry == null)
        {
            _lastOhlcPrice = ohlcPrice;
            //entry.Portfolio = Portfolio with { };
            entries.Add(entry);
            return;
        }

        entry.Return = (price - lastEntry.Price) / lastEntry.Price;

        // copy over most of the states from exitPrice to this
        if (lastEntry.LongCloseType == CloseType.None && lastEntry.ShortCloseType == CloseType.None)
        {
            CopyEntry(entry, lastEntry, security, price);
            //var position = Services.Portfolio.GetCurrentPosition(entry.SecurityId);
            //if (position == null) throw new InvalidOperationException("")
            //position.Notional = CalculateNotional();
        }

        if (IsBackTesting)
        {
            BackTestCheckLongStopLoss(entry, lastEntry, security, ohlcPrice, _intervalType);
            BackTestCheckShortStopLoss(entry, lastEntry, security, ohlcPrice, _intervalType);
        }
        Assertion.ShallNever(entry.SLPrice == 0 && (entry.IsLong || entry.IsShort));

        var toLong = Algorithm.IsOpenLongSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
        var toCloseLong = Algorithm.IsCloseLongSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
        entry.LongSignal = toLong ? SignalType.Open : toCloseLong ? SignalType.Close : SignalType.Hold;

        TryOpenLong(entry, lastEntry, security, ohlcPrice, _intervalType, ref entryPositionId);
        TryCloseLong(entry, security, ohlcPrice, _intervalType);

        var toShort = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
        var toCloseShort = Algorithm.IsCloseShortSignal(entry, lastEntry, ohlcPrice, _lastOhlcPrice);
        entry.ShortSignal = toShort ? SignalType.Open : toCloseShort ? SignalType.Close : SignalType.Hold;

        TryOpenShort(entry, lastEntry, security, ohlcPrice, _intervalType, ref entryPositionId);
        TryCloseShort(entry, security, ohlcPrice, _intervalType);

        lastEntry = entry;
        _lastOhlcPrice = ohlcPrice;

        Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);

        //entry.Portfolio = Portfolio with { };

        // Assertion.ShallNever(Services.Portfolio.GetPosition(entry.SecurityId).Notional <= 0);
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

    private void ClosePositionIfNeeded()
    {
        if (_runningState == AlgoRunningState.Stopped && ShouldCloseOpenPositionsWhenStopped && _openedEntriesBySecurityIds.Count != 0)
        {
            CloseAllOpenPositions();
        }
        else if (_runningState == AlgoRunningState.Halted && ShouldCloseOpenPositionsWhenHalted && _totalCurrentOpenPositions > 0)
        {
            CloseAllOpenPositions();
        }
    }

    private bool CanAcceptPrice()
    {
        var now = DateTime.UtcNow;
        if (_runningState == AlgoRunningState.Running)
        {
            if (DesignatedStopTime <= now)
            {
                _runningState = AlgoRunningState.Stopped;
                return false;
            }
            return true;
        }
        else if (_runningState == AlgoRunningState.NotYetStarted)
        {
            var timeRange = Parameters.TimeRange;
            // handle (and wait for) the start time
            if (DesignatedStartTime == null)
            {
                // one time assignment
                DesignatedStartTime = timeRange.ActualStartTime;
            }
            DateTime start = DesignatedStartTime.Value;

            switch (timeRange.WhenToStart)
            {
                case AlgoStartTimeType.Designated:
                    {
                        if (start.IsValid())
                        {
                            var r = CanStart(start, DesignatedStopTime, now);
                            if (r)
                            {
                                _runningState = AlgoRunningState.Running;
                                _log.Info($"Engine starts running from {AlgoRunningState.NotYetStarted} state, start type is {AlgoStartTimeType.Designated}: {start:yyyyMMdd-HHmmss}");
                                return true;
                            }
                        }
                        else
                        {
                            _log.Error($"Invalid designated algo start time: {start:yyyyMMdd-HHmmss}");
                        }
                        return false;
                    }
                case AlgoStartTimeType.Immediately:
                    _runningState = AlgoRunningState.Running;
                    _log.Info($"Engine starts running immediately.");
                    return true;
                case AlgoStartTimeType.Never:
                    _runningState = AlgoRunningState.Stopped;
                    _log.Info($"Engine never runs even in {AlgoRunningState.NotYetStarted} state.");
                    return false;
                case AlgoStartTimeType.NextStartOf:
                    if (timeRange.NextStartOfIntervalType != null)
                    {
                        var r = CanStart(start, DesignatedStopTime, now);
                        if (r)
                        {
                            _runningState = AlgoRunningState.Running;
                            _log.Info($"Engine starts running from {AlgoRunningState.NotYetStarted} state, start type is {AlgoStartTimeType.NextStartOf}-{timeRange.NextStartOfIntervalType}: {start:yyyyMMdd-HHmmss}");
                            return true;
                        }
                    }
                    else
                    {
                        _log.Error($"Invalid designated algo start time type, missing interval for \"NextStartOf\" type.");
                    }
                    return false;
                case AlgoStartTimeType.NextStartOfLocalDay:
                    {
                        _runningState = AlgoRunningState.Stopped;
                        var localNow = DateTime.Now;
                        if (start > localNow)
                        {
                            var stopTime = DesignatedStopTime;
                            if (stopTime != null)
                                stopTime = stopTime.Value.ToLocalTime();
                            var r = CanStart(start, stopTime, localNow);
                            if (r)
                            {
                                _runningState = AlgoRunningState.Running;
                                return true;
                            }
                        }
                        else
                        {
                            _log.Error($"Invalid designated local algo start time: {start:yyyyMMdd-HHmmss}");
                        }
                        return false;
                    }
                case AlgoStartTimeType.NextMarketOpens:
                    // TODO, need market meta data
                    return false;
                case AlgoStartTimeType.NextWeekMarketOpens:
                    // TODO, need market meta data
                    return false;
                default: return false;
            }
        }
        else if (_runningState == AlgoRunningState.Stopped)
        {
            return false;
        }
        else if (_runningState == AlgoRunningState.Halted)
        {
            // if halted, the designated start time will be effective again
            if (DesignatedStartTime.IsValid() && DesignatedStartTime.Value <= now)
            {
                _runningState = AlgoRunningState.Running;
                _log.Info($"Engine runs again from halted state, designated start time was: {DesignatedStartTime.Value:yyyyMMdd-HHmmss}");
                return true;
            }
            return false;
        }
        return false;

        static bool CanStart(DateTime startTime, DateTime? stopTime, DateTime now)
        {
            return stopTime != null && startTime > stopTime
                ? throw new ArgumentException("Start time is larger than stop time. Program exits.")
                : startTime <= now;
        }
    }

    private void OnNextOrder(Order order)
    {
    }

    private void OnNextTrade(Trade trade)
    {
    }

    private void OnHistoricalPriceEnd(int priceCount)
    {
        AsyncHelper.RunSync(Stop);
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

    //            BackTestCheckLongStopLoss(entry, lastEntry, ohlcPrice, intervalType);
    //            BackTestCheckShortStopLoss(entry, lastEntry, ohlcPrice, intervalType);

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

    private bool BackTestCheckLongStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (IsBackTesting && entry.IsLong && ohlcPrice.L <= entry.SLPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.StopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            Portfolio.Notional = CalculateNotional();
            Portfolio.FreeCash += entry.Notional;

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;

            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private bool BackTestCheckShortStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsShort && ohlcPrice.H >= entry.SLPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.StopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            Portfolio.Notional = CalculateNotional();
            Portfolio.FreeCash += entry.Notional;

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;
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
            var quoteAsset = security.CurrencyAsset;
            if (quoteAsset == null) throw Exceptions.ThrowMissingQuoteAsset(security.Code);
            var assetPosition = Services.Portfolio.GetAsset(quoteAsset.Id);
            if (quoteAsset == null) throw Exceptions.ThrowMissingAssetPosition(security.Code);
            EnterLogic.Open(entry, lastEntry, ohlcPrice.C, endTimeOfBar, sl, tp);
            entry.PositionId = sequenceNum;
            assetPosition.Quantity -= entry.Quantity;
            Portfolio.FreeCash -= entry.Notional;

            _openedEntriesBySecurityIds.GetOrCreate(security.Id)[entry.PositionId] = entry;
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions++;
            _totalOpenedPositions++;
            Algorithm.AfterLongOpened(entry);
            return true;
        }
        return false;
    }

    private bool TryCloseLong(AlgoEntry<T> entry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsLong && entry.LongCloseType == CloseType.None && entry.LongSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingLong(entry);

            ExitLogic.Close(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

            Portfolio.Notional = CalculateNotional();
            Portfolio.FreeCash += entry.Notional;

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;

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

            EnterLogic.Open(entry, lastEntry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType), GetStopLoss(ohlcPrice, security));
            entry.PositionId = sequenceNum;
            Portfolio.FreeCash -= entry.Notional;

            _openedEntriesBySecurityIds.GetOrCreate(security.Id)[entry.PositionId] = entry;
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions++;
            _totalOpenedPositions++;

            Algorithm.AfterShortOpened(entry);
            return true;
        }
        return false;
    }

    private bool TryCloseShort(AlgoEntry<T> entry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (entry.IsShort && entry.ShortCloseType == CloseType.None && entry.ShortSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingShort(entry);

            ExitLogic.Close(this, entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

            Portfolio.Notional = CalculateNotional();
            Portfolio.FreeCash += entry.Notional;

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;

            Algorithm.AfterShortClosed(entry);
            return true;
        }
        return false;
    }

    private decimal CalculateNotional(int securityId)
    {
        if (_openedEntriesBySecurityIds.TryGetValue(securityId, out var algoEntries))
        {

        }
        return Portfolio.FreeCash + _openedEntriesBySecurityIds.Values.SelectMany(i => i.Values).Sum(p => p.Notional);
    }

    private decimal GetStopLoss(OhlcPrice price, Security security)
    {
        return decimal.Round(price.C * (1 - ExitLogic.StopLossRatio), security.PricePrecision, MidpointRounding.ToPositiveInfinity);
    }

    private decimal GetTakeProfit(OhlcPrice price, Security security)
    {
        return decimal.Round(price.C * (1 - ExitLogic.TakeProfitRatio), security.PricePrecision, MidpointRounding.ToNegativeInfinity);
    }

    private DateTime GetOhlcEndTime(OhlcPrice? price, IntervalType intervalType)
    {
        return price == null ? DateTime.MinValue : price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected void CopyEntry(AlgoEntry<T> current, AlgoEntry<T> last, Security security, decimal currentPrice)
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

            _openedEntriesBySecurityIds.GetOrCreate(security.Id)[current.PositionId] = current;
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
        Services.Order.CancelAllOpenOrders();
        Services.Order.CloseAllOpenPositions();
    }
}

public enum AlgoRunningState
{
    NotYetStarted,
    Running,
    Halted,
    Stopped
}