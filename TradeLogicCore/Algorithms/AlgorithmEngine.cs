using Common;
using log4net;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
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
public class AlgorithmEngine<T> : IAlgorithmEngine<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    private readonly Context _context;
    private readonly IServices _services;
    private readonly Persistence _persistence;
    private readonly AutoResetEvent _signal = new(false);

    private readonly int _engineThreadId;

    private readonly IntervalType _intervalType;
    private readonly TimeSpan _interval;
    private readonly IdGenerator _positionIdGen;

    private readonly EngineParameters _engineParameters;

    private IReadOnlyDictionary<int, Security>? _pickedSecurities;

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry<T>?> _lastEntriesBySecurityId = new();

    /// <summary>
    /// Caches last OHLC price. Key is securityId.
    /// </summary>
    private readonly Dictionary<int, OhlcPrice> _lastOhlcPricesBySecurityId = new();

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
    private readonly Dictionary<int, Dictionary<long, AlgoEntry>> _openedEntriesBySecurityIds = new();
    private int _totalCurrentOpenPositions = 0;

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action? ReachedDesignatedEndTime;


    public bool IsBackTesting { get; private set; } = true;

    public IPositionSizingAlgoLogic? Sizing { get; protected set; }

    public IEnterPositionAlgoLogic? EnterLogic { get; protected set; }

    public IExitPositionAlgoLogic? ExitLogic { get; protected set; }

    public ISecurityScreeningAlgoLogic? Screening { get; protected set; }

    public int TotalPriceEventCount { get; protected set; }

    public IAlgorithm<T>? Algorithm { get; private set; }

    public User? User { get; protected set; }

    public Account? Account { get; protected set; }

    public AlgorithmParameters? Parameters { get; private set; }

    public DateTime? DesignatedHaltTime { get; protected set; }

    public DateTime? DesignatedResumeTime { get; protected set; }

    public DateTime? DesignatedStartTime { get; protected set; }

    public DateTime? DesignatedStopTime { get; protected set; }

    public int? HoursBeforeHalt { get; protected set; }

    public IntervalType Interval { get; protected set; }

    public AlgoStopTimeType WhenToStopOrHalt { get; protected set; }

    public int AlgoVersionId { get; private set; }

    public int AlgoBatchId { get; private set; }

    public AlgorithmEngine(Context context, EngineParameters? engineParameters = null)
    {
        _context = context;
        _services = context.Services;
        _persistence = _services.Persistence;
        _engineParameters = engineParameters ?? new EngineParameters();

        _engineThreadId = Environment.CurrentManagedThreadId;

        _positionIdGen = IdGenerators.Get<Position>();
    }

    public void Initialize(IAlgorithm<T> algorithm)
    {
        _context.InitializeAlgorithmContext(this, algorithm);

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;
    }

    public List<AlgoEntry<T>> GetAllEntries(int securityId)
    {
        return _allEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public List<AlgoEntry<T>> GetExecutionEntries(int securityId)
    {
        return _executionEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public Dictionary<long, AlgoEntry> GetOpenEntries(int securityId)
    {
        return _openedEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public void Halt(DateTime? resumeTime)
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

    public async Task<int> Run(AlgorithmParameters parameters)
    {
        if (Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        TotalPriceEventCount = 0;
        User = _services.Admin.CurrentUser;
        Account = _services.Admin.CurrentAccount;
        Parameters = parameters;
        if (User == null || Account == null)
            return 0;
        IsBackTesting = parameters.IsBackTesting;
        Interval = parameters.Interval;
        Parameters = parameters;

        _services.MarketData.NextOhlc -= OnNextPrice;
        _services.MarketData.NextOhlc += OnNextPrice;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        _services.MarketData.HistoricalPriceEnd += OnHistoricalPriceEnd;
        _services.Order.NextOrder -= OnNextOrder;
        _services.Order.NextOrder += OnNextOrder;
        _services.Trade.NextTrade -= OnNextTrade;
        _services.Trade.NextTrade += OnNextTrade;

        // close open positions
        if (_engineParameters.CloseOpenPositionsOnStart)
            CloseAllOpenPositions();

        // pick security to trade
        // check associated asset's position, and map any existing position into algo entry
        // then register market quote feed if all good
        Screening.SetAndPick(parameters.SecurityPool.ToDictionary(s => s.Id, s => s));
        var subscriptionCount = 0;
        var pickedSecurities = Screening.GetAll();
        foreach (var security in pickedSecurities.Values)
        {
            var currencyAsset = security.EnsureCurrencyAsset();
            var assetPosition = _services.Portfolio.GetAssetBySecurityId(currencyAsset.Id);
            if (assetPosition == null || assetPosition.Quantity <= 0)
            {
                _log.Warn($"Cannot trade the picked security {security.Code}; the account may not have enough free asset to trade.");
                continue;
            }
            await _services.MarketData.SubscribeOhlc(security, Interval);
            subscriptionCount++;
        }
        SetAlgoEffectiveTimeRange(parameters.TimeRange);

        // prepare algo entry related info
        AlgoVersionId = DateTime.UtcNow.Date.ToDateNumber();
        var (table, database) = DatabaseNames.GetTableAndDatabaseName<AlgoEntry>();
        AlgoBatchId = Convert.ToInt32(await _context.Storage.GetMax(nameof(AlgoEntry.BatchId), table, database));

        // wait for the price thread to be stopped by unsubscription or forceful algo exit
        _signal.WaitOne();

        _log.Info("Algorithm Engine execution ends, processed " + TotalPriceEventCount);
        return TotalPriceEventCount;
    }

    public async Task Stop()
    {
        if (Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        _log.Info("Algorithm Engine is shutting down.");

        _runningState = AlgoRunningState.Stopped;
        _services.MarketData.NextOhlc -= OnNextPrice;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        await _services.MarketData.UnsubscribeAllOhlcs();
        var securities = Screening.GetAll();
        foreach (var security in securities.Values)
        {
            await _services.MarketData.UnsubscribeOhlc(security, Interval);
        }
        // unblock the main thread and let it finish
        _signal.Set();
    }

    private void SetAlgoEffectiveTimeRange(AlgoEffectiveTimeRange timeRange)
    {
        var now = DateTime.UtcNow;
        DateTime? stopTime;

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
    }

    /// <summary>
    /// Handler which is invoked when price feed notifies a new price object arrives.
    /// We expect this is a separated thread from the original engine.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    /// <param name="isComplete"></param>
    private void OnNextPrice(int securityId, OhlcPrice ohlcPrice, bool isComplete)
    {
        _log.Info(ohlcPrice);

        ClosePositionIfNeeded();

        if (!CanAcceptPrice())
            return;
        if (!isComplete)
            return;

        TotalPriceEventCount++;

#if DEBUG
        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId != threadId);
#endif
        Update(securityId, ohlcPrice);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    public async Task Update(int securityId, OhlcPrice ohlcPrice)
    {
        if (Algorithm == null || Parameters == null || Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (Screening.TryCheckIfChanged(out var securities) || _pickedSecurities == null)
        {
            _pickedSecurities = securities;
        }
        if (!_pickedSecurities.TryGetValue(securityId, out var security))
        {
            return;
        }

        Algorithm.BeforeProcessingSecurity(security);

        var entries = _allEntriesBySecurityIds.GetOrCreate(security.Id);
        var lastEntry = _lastEntriesBySecurityId.GetValueOrDefault(security.Id);
        var price = ohlcPrice.C;

        var entryPositionId = lastEntry?.PositionId ?? _positionIdGen.NewTimeBasedId;
        var entry = new AlgoEntry<T>
        {
            AlgoId = Algorithm.Id,
            VersionId = Algorithm.VersionId,
            BatchId = AlgoBatchId,
            SecurityId = securityId,
            Security = security,
            PositionId = entryPositionId,
            Time = ohlcPrice.T,
            Variables = Algorithm.CalculateVariables(price, lastEntry),
            Price = price,
        };

        if (lastEntry == null)
        {
            lastEntry = entry;
            _lastOhlcPricesBySecurityId[securityId] = ohlcPrice;
            _lastEntriesBySecurityId[securityId] = lastEntry;
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

        // if opened, SL must be set
        Assertion.ShallNever((entry.IsLong || entry.IsShort) && entry.StopLossPrice == 0);

        var lastOhlcPrice = _lastOhlcPricesBySecurityId[securityId];
        await TryLong(ohlcPrice, security, entry, lastEntry, lastOhlcPrice);
        await TryShort(ohlcPrice, security, entry, lastEntry, lastOhlcPrice);

        _log.Info(entry);
        lastEntry = entry;
        _lastOhlcPricesBySecurityId[securityId] = ohlcPrice;

        entries.Add(entry);

        if (lastEntry != null && lastEntry.IsLong)
        {
            _log.Info("Close any opened entry at the end of back-testing.");
            await TryCloseLong(entry, security, ohlcPrice, _intervalType);
            TryCloseShort(entry, security, ohlcPrice, _intervalType);

            //Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);
        }

        if (lastEntry != null && lastEntry.IsShort)
        {
            _log.Info("Discard any opened entry at the end of back-testing.");
            throw new NotImplementedException();
        }

        Algorithm.AfterProcessingSecurity(security);
    }

    private void ClosePositionIfNeeded()
    {
        if (_runningState == AlgoRunningState.Stopped && _engineParameters.CloseOpenPositionsOnStop && _openedEntriesBySecurityIds.Count != 0)
        {
            CloseAllOpenPositions();
        }
        else if (_runningState == AlgoRunningState.Halted && _engineParameters.CloseOpenPositionsOnStop && _totalCurrentOpenPositions > 0)
        {
            CloseAllOpenPositions();
        }
    }

    private bool CanAcceptPrice()
    {
        if (Parameters == null) throw Exceptions.InvalidAlgorithmEngineState();

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
        if (order.Status == OrderStatus.Live)
        {
        }
        else if (order.Status == OrderStatus.Cancelled)
        {

        }
        else if (order.Status == OrderStatus.PartialFilled)
        {

        }
        else if (order.Status == OrderStatus.Filled)
        {
            var lastEntry = _lastEntriesBySecurityId.GetValueOrDefault(order.SecurityId);
        }
        _persistence.Enqueue(order);
    }

    private void OnNextTrade(Trade trade)
    {
        // TODO
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

    //            Assertion.ShallNever(entry.StopLossPrice == 0 && (entry.IsLong || entry.IsShort));

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

    /// <summary>
    /// Check if it is time to proactively open or close a long position.
    /// </summary>
    /// <param name="ohlcPrice"></param>
    /// <param name="security"></param>
    /// <param name="entry"></param>
    /// <param name="lastEntry"></param>
    /// <param name="lastOhlcPrice"></param>
    private async Task TryLong(OhlcPrice ohlcPrice, Security security, AlgoEntry<T> entry, AlgoEntry<T>? lastEntry, OhlcPrice lastOhlcPrice)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        var toLong = Algorithm.IsOpenLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
        var toCloseLong = Algorithm.IsCloseLongSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
        entry.LongSignal = toLong ? SignalType.Open : toCloseLong ? SignalType.Close : SignalType.Hold;

        await TryOpenLong(entry, lastEntry, security, ohlcPrice, _intervalType);
        await TryCloseLong(entry, security, ohlcPrice, _intervalType);
    }

    /// <summary>
    /// Check if it is time to proactively open or close a short position.
    /// </summary>
    /// <param name="ohlcPrice"></param>
    /// <param name="security"></param>
    /// <param name="entry"></param>
    /// <param name="lastEntry"></param>
    /// <param name="lastOhlcPrice"></param>
    private async Task TryShort(OhlcPrice ohlcPrice, Security security, AlgoEntry<T> entry, AlgoEntry<T>? lastEntry, OhlcPrice lastOhlcPrice)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        var toShort = Algorithm.IsShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
        var toCloseShort = Algorithm.IsCloseShortSignal(entry, lastEntry, ohlcPrice, lastOhlcPrice);
        entry.ShortSignal = toShort ? SignalType.Open : toCloseShort ? SignalType.Close : SignalType.Hold;

        await TryOpenShort(entry, lastEntry, security, ohlcPrice, _intervalType);
        await TryCloseShort(entry, security, ohlcPrice, _intervalType);
    }

    private async Task<bool> TryOpenLong(AlgoEntry<T> entry, AlgoEntry<T>? lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!entry.IsLong && entry.LongCloseType == CloseType.None && entry.LongSignal == SignalType.Open)
        {
            Algorithm.BeforeOpeningLong(entry);

            var endTimeOfBar = GetOhlcEndTime(ohlcPrice, intervalType);
            var sl = GetStopLoss(ohlcPrice, Side.Buy, security);
            var tp = GetTakeProfit(ohlcPrice, Side.Buy, security);
            var assetPosition = _services.Portfolio.GetPositionRelatedQuoteBalance(entry.SecurityId);

            if (IsBackTesting)
            {
                EnterLogic.BackTestOpen(entry, lastEntry, ohlcPrice.C, Side.Buy, endTimeOfBar, sl, tp);
                //Services.Portfolio.SpendAsset(entry.SecurityId, entry.Notional);
                entry.PositionId = _positionIdGen.NewInt;

                _openedEntriesBySecurityIds.GetOrCreate(security.Id)[entry.PositionId] = entry;
                _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
                _totalCurrentOpenPositions++;
            }
            else
            {
                await EnterLogic.Open(entry, lastEntry, ohlcPrice.C, Side.Buy, endTimeOfBar, sl, tp);
            }

            Algorithm.AfterLongOpened(entry);
            return true;
        }
        return false;
    }

    private async Task<bool> TryCloseLong(AlgoEntry<T> entry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (entry.IsLong && entry.LongCloseType == CloseType.None && entry.LongSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingLong(entry);

            if (IsBackTesting)
            {
                ExitLogic.BackTestClose(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
                //Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);
            }
            else
            {
                await ExitLogic.Close(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
            }
            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;


            Algorithm.AfterLongClosed(entry);
            return true;
        }
        return false;
    }

    private async Task<bool> TryOpenShort(AlgoEntry<T> entry, AlgoEntry<T>? lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!entry.IsShort && entry.ShortCloseType == CloseType.None && entry.ShortSignal == SignalType.Open)
        {
            Algorithm.BeforeOpeningShort(entry);
            var endTimeOfBar = GetOhlcEndTime(ohlcPrice, intervalType);
            var sl = GetStopLoss(ohlcPrice, Side.Sell, security);
            var tp = GetTakeProfit(ohlcPrice, Side.Sell, security);

            if (IsBackTesting)
            {
                EnterLogic.BackTestOpen(entry, lastEntry, ohlcPrice.C, Side.Sell, endTimeOfBar, sl, tp);
                //Services.Portfolio.SpendAsset(entry.SecurityId, entry.Notional);
                entry.PositionId = _positionIdGen.NewInt;

                _openedEntriesBySecurityIds.GetOrCreate(security.Id)[entry.PositionId] = entry;
                _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
                _totalCurrentOpenPositions++;
            }
            else
            {
                await EnterLogic.Open(entry, lastEntry, ohlcPrice.C, Side.Sell, endTimeOfBar, sl, tp);
            }
            Algorithm.AfterShortOpened(entry);
            return true;
        }
        return false;
    }

    private async Task<bool> TryCloseShort(AlgoEntry<T> entry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (entry.IsShort && entry.ShortCloseType == CloseType.None && entry.ShortSignal == SignalType.Close)
        {
            Algorithm.BeforeClosingShort(entry);

            if (IsBackTesting)
                ExitLogic.BackTestClose(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
            else
                await ExitLogic.Close(entry, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
            //Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;

            Algorithm.AfterShortClosed(entry);
            return true;
        }
        return false;
    }

    private bool BackTestCheckLongStopLoss(AlgoEntry<T> entry, AlgoEntry<T> lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (IsBackTesting && entry.IsLong && ohlcPrice.L <= entry.StopLossPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            //Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);

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
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (IsBackTesting && entry.IsShort && ohlcPrice.H >= entry.StopLossPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            //Services.Portfolio.Realize(entry.SecurityId, entry.RealizedPnl);

            _openedEntriesBySecurityIds.GetValueOrDefault(security.Id)?.Remove(entry.PositionId);
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            _totalCurrentOpenPositions--;
            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private decimal GetStopLoss(OhlcPrice price, Side side, Security security)
    {
        if (ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        decimal slRatio = side switch
        {
            Side.Buy => ExitLogic.LongStopLossRatio,
            Side.Sell => ExitLogic.ShortStopLossRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        return decimal.Round(price.C * (1 - slRatio), security.PricePrecision, MidpointRounding.ToPositiveInfinity);
    }

    private decimal GetTakeProfit(OhlcPrice price, Side side, Security security)
    {
        if (ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        decimal tpRatio = side switch
        {
            Side.Buy => ExitLogic.LongTakeProfitRatio,
            Side.Sell => ExitLogic.ShortTakeProfitRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        return decimal.Round(price.C * (1 + tpRatio), security.PricePrecision, MidpointRounding.ToNegativeInfinity);
    }

    private static DateTime GetOhlcEndTime(OhlcPrice price, IntervalType intervalType)
    {
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected void CopyEntry(AlgoEntry current, AlgoEntry last, Security security, decimal currentPrice)
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
            current.StopLossPrice = last.StopLossPrice;
            current.TakeProfitPrice = last.TakeProfitPrice;
            current.UnrealizedPnl = (currentPrice - current.EnterPrice!.Value) * current.Quantity;
            current.Fee = last.Fee;

            Assertion.Shall(current.EnterPrice.HasValue);
            Assertion.Shall(current.StopLossPrice.HasValue);
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
            current.StopLossPrice = null;
            current.TakeProfitPrice = null;
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
        if (Parameters == null) throw Exceptions.InvalidAlgorithmEngineState();

        var securities = Parameters.SecurityPool;
        foreach (var security in securities)
        {
            _services.Order.CancelAllOpenOrders(security);
        }
        _services.Portfolio.CloseAllPositions();
    }
}
