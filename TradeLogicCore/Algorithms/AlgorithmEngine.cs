using Common;
using log4net;
using TradeCommon.Algorithms;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;
public class AlgorithmEngine : IAlgorithmEngine
{
    private static readonly ILog _log = Logger.New();

    private readonly Context _context;
    private readonly IServices _services;
    private readonly Persistence _persistence;
    private readonly AutoResetEvent _signal = new(false);

    private readonly int _engineThreadId;

    private readonly IntervalType _intervalType;

    private IReadOnlyDictionary<int, Security>? _pickedSecurities;


    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry?> _lastEntriesBySecurityId = new();

    /// <summary>
    /// Caches last OHLC price. Key is securityId.
    /// </summary>
    private readonly Dictionary<int, OhlcPrice> _lastOhlcPricesBySecurityId = new();

    /// <summary>
    /// Caches full history of entries.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry>> _allEntriesBySecurityIds = new();

    /// <summary>
    /// Caches entries related to execution only.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry>> _executionEntriesBySecurityIds = new();

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action? ReachedDesignatedEndTime;

    public EngineParameters EngineParameters { get; private set; }

    public bool IsBackTesting { get; private set; } = true;

    public IPositionSizingAlgoLogic? Sizing { get; protected set; }

    public IEnterPositionAlgoLogic? EnterLogic { get; protected set; }

    public IExitPositionAlgoLogic? ExitLogic { get; protected set; }

    public ISecurityScreeningAlgoLogic? Screening { get; protected set; }

    public int TotalPriceEventCount { get; protected set; }

    public AlgoBatch AlgoBatch { get; private set; }

    public IAlgorithm Algorithm { get; private set; }

    public User? User { get; protected set; }

    public Account? Account { get; protected set; }

    public AlgorithmParameters? AlgoParameters { get; private set; }

    public DateTime? DesignatedHaltTime { get; protected set; }

    public DateTime? DesignatedResumeTime { get; protected set; }

    public DateTime? DesignatedStartTime { get; protected set; }

    public DateTime? DesignatedStopTime { get; protected set; }

    public int? HoursBeforeHalt { get; protected set; }

    public IntervalType Interval { get; protected set; }

    public AlgoStopTimeType WhenToStopOrHalt { get; protected set; }

    public int AlgoVersionId { get; private set; }

    public int AlgoBatchId { get; private set; }

    public AlgorithmEngine(Context context, EngineParameters engineParameters)
    {
        _context = context;
        _services = context.Services;
        _persistence = _services.Persistence;

        EngineParameters = engineParameters;

        _engineThreadId = Environment.CurrentManagedThreadId;
    }

    public void Initialize(IAlgorithm algorithm)
    {
        _context.InitializeAlgorithmContext(this, algorithm);

        AlgoBatch = _context.SaveAlgoBatch();

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;
    }

    public List<AlgoEntry> GetAllEntries(int securityId)
    {
        return _allEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
    }

    public List<AlgoEntry> GetExecutionEntries(int securityId)
    {
        return _executionEntriesBySecurityIds.GetValueOrDefault(securityId) ?? new();
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
        AlgoParameters = parameters;
        if (User == null || Account == null)
            return 0;
        IsBackTesting = parameters.IsBackTesting;
        Interval = parameters.Interval;
        AlgoParameters = parameters;

        // close positions need these events
        _services.Portfolio.PositionClosed -= OnPositionClosed;
        _services.Portfolio.PositionClosed += OnPositionClosed;

        // pick security to trade
        // check associated asset's position, and map any existing position into algo entry
        // then register market quote feed if all good
        Screening.SetAndPick(parameters.SecurityPool.ToDictionary(s => s.Id, s => s));

        // cancel live orders
        if (EngineParameters.CancelOpenOrdersOnStart)
        {
            var securities = AlgoParameters.SecurityPool;
            foreach (var security in securities)
            {
                await _services.Order.CancelAllOpenOrders(security);
            }
        }
        // close open positions
        if (EngineParameters.CloseOpenPositionsOnStart)
        {
            await _services.Portfolio.CloseAllOpenPositions(Comments.CloseAllBeforeStart);
        }

        // subscribe to events
        _services.MarketData.NextOhlc -= OnNextOhlcPrice;
        _services.MarketData.NextOhlc += OnNextOhlcPrice;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        _services.MarketData.HistoricalPriceEnd += OnHistoricalPriceEnd;

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

        // wait for the price thread to be stopped by unsubscription or forceful algo exit
        _signal.WaitOne();

        _log.Info("Algorithm Engine execution ends, processed " + TotalPriceEventCount);
        return TotalPriceEventCount;
    }

    private void OnPositionClosed(Position position)
    {
        Algorithm.NotifyPositionChanged(position);
        _log.Info("------- TEST MSG: now should be ok to trade again.");
    }

    public async Task Stop()
    {
        if (Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        _log.Info("Algorithm Engine is shutting down.");

        _runningState = AlgoRunningState.Stopped;
        _services.MarketData.NextOhlc -= OnNextOhlcPrice;
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

    private async void OnNextOhlcPrice(int securityId, OhlcPrice price, bool isComplete)
    {
        await ProcessPrice(securityId, price, isComplete);
    }

    /// <summary>
    /// Handler which is invoked when price feed notifies a new price object arrives.
    /// We expect this is a separated thread from the original engine.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    /// <param name="isComplete"></param>
    private async Task ProcessPrice(int securityId, OhlcPrice ohlcPrice, bool isComplete)
    {
        await ClosePositionIfNeeded();

        if (!CanAcceptPrice())
            return;

        if (!isComplete)
            return;

        TotalPriceEventCount++;

#if DEBUG
        var threadId = Environment.CurrentManagedThreadId;
        Assertion.Shall(_engineThreadId != threadId);
#endif

        if (Screening.TryCheckIfChanged(out var securities) || _pickedSecurities == null)
            _pickedSecurities = securities;

        // only allow an order to be alive for one time interval
        var security = _pickedSecurities?.GetOrDefault(securityId);
        //ExpireOpenOrders(security);

        _log.Info($"\n\tPRX: [{ohlcPrice.T:HHmmss}][{security.Code}]\n\t\tH:{security.RoundTickSize(ohlcPrice.H)}, L:{security.RoundTickSize(ohlcPrice.L)}, C:{security.RoundTickSize(ohlcPrice.C)}, V:{ohlcPrice.V}");

        await Update(securityId, ohlcPrice);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    public async Task Update(int securityId, OhlcPrice ohlcPrice)
    {
        if (Algorithm == null || AlgoParameters == null || Screening == null || AlgoBatch == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!_pickedSecurities.TryGetValue(securityId, out var security))
            return;

        Algorithm.BeforeProcessingSecurity(security);

        var entries = _allEntriesBySecurityIds.GetOrCreate(security.Id);
        var last = _lastEntriesBySecurityId.GetValueOrDefault(security.Id);
        var price = ohlcPrice.C;

        var current = new AlgoEntry
        {
            AlgoBatchId = AlgoBatch.Id,
            SecurityId = securityId,
            Security = security,
            PositionId = 0,
            Time = ohlcPrice.T,
            Variables = Algorithm.CalculateVariables(price, last),
            Price = price,
        };
        _services.Security.Fix(current, security);

        if (last == null)
        {
            last = current;
            _lastOhlcPricesBySecurityId[securityId] = ohlcPrice;
            _lastEntriesBySecurityId[securityId] = last;
            entries.Add(current);
            return;
        }

        current.Return = (price - last.Price) / last.Price;

        // copy over most of the states from exitPrice to this
        if (last.LongCloseType == CloseType.None && last.ShortCloseType == CloseType.None)
        {
            CopyEntry(current, last, security, price);
        }

        _lastEntriesBySecurityId[security.Id] = current;
        _lastOhlcPricesBySecurityId[securityId] = ohlcPrice;

        if (IsBackTesting)
        {
            BackTestCheckLongStopLoss(current, last, security, ohlcPrice, _intervalType);
            BackTestCheckShortStopLoss(current, last, security, ohlcPrice, _intervalType);
        }

        var lastOhlcPrice = _lastOhlcPricesBySecurityId[securityId];
        Algorithm.Analyze(current, last, ohlcPrice, lastOhlcPrice);

        // determine whethet to cancel partially filled or live orders (limit orders may live for a very long time)
        if (await TryCloseOpenOrders(current))
        {
            _log.Info($"\n\tORD: [{current.Time:HHmmss}][{current.SecurityCode}][CloseOpened]");
        }

        // try to open or close long or short
        if (await TryOpenLong(current, last, ohlcPrice, lastOhlcPrice))
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][OpenLong]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
        }
        else if (await TryCloseLong(current, ohlcPrice, _intervalType))
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][CloseLong]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
        }
        else if (await TryOpenShort(current, last, ohlcPrice, lastOhlcPrice))
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][OpenShort]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
        }
        else if (await TryCloseShort(current, ohlcPrice, _intervalType))
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][CloseShort]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
        }
        else
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
        }
        entries.Add(current);


        Algorithm.AfterProcessingSecurity(security);
    }

    private async Task ClosePositionIfNeeded()
    {
        if (_runningState == AlgoRunningState.Stopped || _runningState == AlgoRunningState.Halted)
        {
            if (EngineParameters.CloseOpenPositionsOnStop && _services.Portfolio.HasPosition)
            {
                await _services.Portfolio.CloseAllOpenPositions(Comments.CloseAllBeforeStop);
            }
        }
    }

    private bool CanAcceptPrice()
    {
        if (AlgoParameters == null) throw Exceptions.InvalidAlgorithmEngineState();

        var now = DateTime.UtcNow;
        switch (_runningState)
        {
            case AlgoRunningState.Running:
                if (DesignatedStopTime <= now)
                {
                    _runningState = AlgoRunningState.Stopped;
                    return false;
                }
                return true;
            case AlgoRunningState.NotYetStarted:
                {
                    var timeRange = AlgoParameters.TimeRange;
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

            case AlgoRunningState.Stopped:
                return false;
            case AlgoRunningState.Halted:
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

    private void OnHistoricalPriceEnd(int priceCount)
    {
        AsyncHelper.RunSync(Stop);
    }

    private async Task<bool> TryCloseOpenOrders(AlgoEntry current)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        var openOrders = Algorithm.PickOpenOrdersToCleanUp(current);
        if (!openOrders.IsNullOrEmpty())
        {
            var hasRealOrders = openOrders.Any(o => o.Type is OrderType.Limit or OrderType.Market);
            if (hasRealOrders)
            {
                foreach (var openOrder in openOrders)
                {
                    await _services.Order.CancelOrder(openOrder);
                }
                return true;
            }
        }
        return false;
    }

    private async Task<bool> TryOpenLong(AlgoEntry current, AlgoEntry last, OhlcPrice price, OhlcPrice lastPrice)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        var security = current.Security;

        if (!Algorithm.CanOpenLong(current))
            return false;

        var time = DateTime.UtcNow;
        var enterSide = Side.Buy;
        var sl = GetStopLossLimitPrice(price.C, enterSide, security);
        var tp = GetTakeProfitLimitPrice(price.C, enterSide, security);

        Algorithm.BeforeOpeningLong(current);

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
            EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
        else
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][OpenLong]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
            await EnterLogic.Open(current, last, price.C, enterSide, time, sl, tp);
        }
        _persistence.Insert(current);
        Algorithm.AfterLongOpened(current);
        return true;
    }

    private async Task<bool> TryCloseLong(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!Algorithm.CanCloseLong(current))
            return false;

        Algorithm.BeforeClosingLong(current);
        var security = current.Security;

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
        {
            ExitLogic.BackTestClose(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
        }
        else
        {
            await ExitLogic.Close(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
        }

        _persistence.Insert(current);
        Algorithm.AfterLongClosed(current);
        return true;
    }

    private async Task<bool> TryOpenShort(AlgoEntry current, AlgoEntry last, OhlcPrice price, OhlcPrice lastPrice)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        var security = current.Security;

        if (!Algorithm.CanOpenShort(current))
            return false;

        Algorithm.BeforeOpeningShort(current);

        var time = DateTime.UtcNow;
        var enterSide = Side.Sell;
        var sl = GetStopLossLimitPrice(price.C, enterSide, security);
        var tp = GetTakeProfitLimitPrice(price.C, enterSide, security);

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
            EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
        else
        {
            _log.Info($"\n\tAE:  [{current.Time:HHmmss}][{current.SecurityCode}][OpenShort]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
            await EnterLogic.Open(current, last, price.C, enterSide, time, sl, tp);
        }
        _persistence.Insert(current);
        Algorithm.AfterShortOpened(current);
        return true;
    }

    private async Task<bool> TryCloseShort(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        var security = current.Security;
        if (!Algorithm.CanCloseShort(current))
            return false;

        Algorithm.BeforeClosingShort(current);

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
            ExitLogic.BackTestClose(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
        else
            await ExitLogic.Close(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));

        _persistence.Insert(current);
        Algorithm.AfterShortClosed(current);
        return true;
    }

    private bool BackTestCheckLongStopLoss(AlgoEntry entry, AlgoEntry lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasLongPosition = _services.Portfolio.GetOpenPositionSide(entry.SecurityId) == Side.Buy;
        var stopLossPrice = GetStopLossLimitPrice(entry.EnterPrice.Value, Side.Buy, entry.Security);
        if (IsBackTesting && hasLongPosition && ohlcPrice.L <= stopLossPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);

            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private bool BackTestCheckShortStopLoss(AlgoEntry entry, AlgoEntry lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        if (entry.EnterPrice == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasShortPosition = _services.Portfolio.GetOpenPositionSide(entry.SecurityId) == Side.Sell;
        var stopLossPrice = GetStopLossLimitPrice(entry.EnterPrice.Value, Side.Sell, entry.Security);
        if (IsBackTesting && hasShortPosition && ohlcPrice.H >= stopLossPrice)
        {
            Algorithm.BeforeStopLossLong(entry);

            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);

            Algorithm.AfterStopLossLong(entry);
            return true;
        }
        return false;
    }

    private decimal GetStopLossLimitPrice(decimal price, Side parentOrderSide, Security security)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        decimal slRatio = parentOrderSide switch
        {
            Side.Buy => Algorithm.LongStopLossRatio,
            Side.Sell => -Algorithm.ShortStopLossRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        if (!slRatio.IsValid() && slRatio <= 0)
            return 0;
        return security.GetStopLossPrice(price, slRatio);
    }

    private decimal GetTakeProfitLimitPrice(decimal price, Side parentOrderSide, Security security)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        decimal tpRatio = parentOrderSide switch
        {
            Side.Buy => Algorithm.LongTakeProfitRatio,
            Side.Sell => -Algorithm.ShortTakeProfitRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        if (!tpRatio.IsValid() && tpRatio <= 0)
            return 0;
        return security.GetTakeProfitPrice(price, tpRatio);
    }

    private static DateTime GetOhlcEndTime(OhlcPrice price, IntervalType intervalType)
    {
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected void CopyEntry(AlgoEntry current, AlgoEntry last, Security security, decimal currentPrice)
    {
        //current.IsLong = last.IsLong;
        //current.IsShort = last.IsShort;
        var position = _services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        if (position != null && !position.IsClosed)
        {
            current.Quantity = position.Quantity;
            current.EnterPrice = position.Side == Side.Buy ? position.LongPrice : position.ShortPrice;
            current.EnterTime = position.CreateTime;
            current.ExitPrice = position.Side == Side.Buy ? position.ShortPrice : position.LongPrice;
            current.Elapsed = position.UpdateTime - position.CreateTime;
            //current.StopLossPrice = last.StopLossPrice;
            //current.TakeProfitPrice = last.TakeProfitPrice;
            current.Fee = last.Fee;
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
            //current.StopLossPrice = null;
            //current.TakeProfitPrice = null;
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
