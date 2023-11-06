using Common;
using log4net;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
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
using TradeDataCore.Essentials;
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
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry?> _currentEntriesBySecurityId = new();

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

    private readonly IdGenerator _algoEntryIdGen;

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action? ReachedDesignatedEndTime;

    public string Id => AlgoBatch.Id;

    public bool IsBackTesting { get; private set; } = true;

    public EngineParameters EngineParameters { get; private set; }

    public IPositionSizingAlgoLogic? Sizing { get; protected set; }

    public IEnterPositionAlgoLogic? EnterLogic { get; protected set; }

    public IExitPositionAlgoLogic? ExitLogic { get; protected set; }

    public ISecurityScreeningAlgoLogic? Screening { get; protected set; }

    public int TotalPriceEventCount { get; protected set; }

    public AlgoBatch AlgoBatch { get; private set; }

    public Algorithm Algorithm { get; private set; }

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

    public AlgorithmEngine(Context context, Algorithm algorithm, EngineParameters engineParameters)
    {
        _context = context;
        _services = context.Services;
        _persistence = _services.Persistence;

        EngineParameters = engineParameters;

        _context.SetPreferredQuoteCurrencies(EngineParameters.PreferredQuoteCurrencies);
        _context.SetGlobalCurrencyFilter(EngineParameters.GlobalCurrencyFilter);
        _context.InitializeAlgorithmContext(this, algorithm);

        _algoEntryIdGen = IdGenerators.Get<AlgoEntry>();
        _engineThreadId = Environment.CurrentManagedThreadId;

        _services.Order.OrderProcessed += OnOrderProcessed;
        _services.Trade.TradeProcessed += OnTradeProcessed;
        _services.Portfolio.PositionProcessed += OnPositionProcessed;

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
        if (parameters.Interval.IsUnknown()) throw Exceptions.Invalid("Invalid interval type.");

        TotalPriceEventCount = 0;
        User = _services.Admin.CurrentUser;
        Account = _services.Admin.CurrentAccount;
        AlgoParameters = parameters;
        IsBackTesting = parameters.IsBackTesting;
        Interval = parameters.Interval;

        if (User == null || Account == null)
            return 0;

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
                await _services.Order.CancelAllOpenOrders(security, OrderActionType.CleanUpLive, true);
            }
        }

        await InitializeCaches(false);

        var hasPositionChangedDuringInit = false;
        // close open positions, when do not assume no open position
        if (!EngineParameters.AssumeNoOpenPositionOnStart && EngineParameters.CloseOpenPositionsOnStart)
        {
            if (await _services.Portfolio.CloseAllOpenPositions(Comments.CloseAllBeforeStart))
            {
                hasPositionChangedDuringInit = true;
            }
        }
        // sell non-cash assets
        if (!EngineParameters.AssumeNoOpenPositionOnStart && EngineParameters.CleanUpNonCashOnStart)
        {
            if (await _services.Portfolio.CleanUpNonCashAssets("Clean up assets before engine starts."))
            {
                hasPositionChangedDuringInit = true;
            }
        }
        if (hasPositionChangedDuringInit || EngineParameters.AssumeNoOpenPositionOnStart)
        {
            // refresh the cache again
            await InitializeCaches(EngineParameters.AssumeNoOpenPositionOnStart);
        }

        // subscribe to events
        _services.MarketData.NextOhlc -= OnNextOhlcPrice;
        _services.MarketData.NextOhlc += OnNextOhlcPrice;
        _services.MarketData.NextTick -= OnNextTickPrice;
        _services.MarketData.NextTick += OnNextTickPrice;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;
        _services.MarketData.HistoricalPriceEnd += OnHistoricalPriceEnd;

        // subscribe to prices
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
            await _services.MarketData.SubscribeTick(security);
            subscriptionCount++;
        }
        SetAlgoEffectiveTimeRange(parameters.TimeRange);

        // wait for the price thread to be stopped by unsubscription or forceful algo exit
        _signal.WaitOne();

        _log.Info("Algorithm Engine execution ends, processed " + TotalPriceEventCount);
        return TotalPriceEventCount;
    }

    private async Task InitializeCaches(bool assumeNoOpenPosition)
    {
        _services.Order.Reset();
        _services.Trade.Reset();
        _services.Portfolio.Reset(true, true, true);

        var assets = await _context.Storage.ReadAssets();
        _services.Security.Fix(assets);
        _services.Portfolio.Update(assets, true);

        if (!assumeNoOpenPosition)
        {
            var positions = await _services.Portfolio.GetStoragePositions(DateUtils.TMinus(Consts.LookbackDayCount), OpenClose.OpenOnly);
            _services.Security.Fix(positions);

            List<Trade> trades = new();
            foreach (var position in positions)
            {
                trades.AddRange(await _context.Storage.ReadTradesByPositionId(position.Security, position.Id, OperatorType.Equals));
            }
            _services.Security.Fix(trades);

            List<Order> orders = new();
            foreach (var group in trades.GroupBy(t => t.SecurityId))
            {
                var security = await _services.Security.GetSecurity(group.Key);
                orders.AddRange(await _context.Storage.ReadOrders(security!, group.Select(t => t.OrderId).ToList()));
            }
            _services.Security.Fix(orders);

            _services.Order.Update(orders);
            _services.Trade.Update(trades);
            _services.Portfolio.Update(positions, true);
        }
    }

    private void OnOrderProcessed(Order order)
    {
        var filledQtyStr = order.Status == OrderStatus.PartialFilled ? ", FILLQTY:" + order.FormattedFilledQuantity : "";
        var orderTypeStr = "";
        var orderPriceStr = "";
        if (order.Type is OrderType.StopLimit or OrderType.Stop)
        {
            orderTypeStr = "SL";
            orderPriceStr = order.FormattedStopPrice.ToString();
        }
        else if (order.Type is OrderType.TakeProfitLimit or OrderType.TakeProfit)
        {
            orderTypeStr = "TP";
            orderPriceStr = order.FormattedStopPrice.ToString();
        }
        else
        {
            orderPriceStr = order.FormattedPrice.ToString();
        }
        _log.Info($"\n\tORD: [{order.UpdateTime:HHmmss}][{order.SecurityCode}][{order.Type}][{order.Action}][{order.Status}][{order.Side}]\n\t\tID:{order.Id}, {orderTypeStr}P:{orderPriceStr}, Q:{order.FormattedQuantity}{filledQtyStr}");
    }

    private void OnTradeProcessed(Trade trade)
    {
        _log.Info($"\n\tTRD: [{trade.Time:HHmmss}][{trade.SecurityCode}][{trade.Side}]\n\t\tID:{trade.Id}, P:{trade.Price}, Q:{trade.Quantity}");
    }

    private void OnPositionProcessed(Position position, Trade trade)
    {
        if (_runningState == AlgoRunningState.NotYetStarted)
            return;

        var current = _currentEntriesBySecurityId.ThreadSafeGet(position.SecurityId)!;
        if (position.IsClosed)
        {
            var initialQuoteAsset = _services.Portfolio.InitialPortfolio.GetAssetBySecurityId(position.Security.QuoteSecurity.Id);
            var currentQuoteAsset = _services.Portfolio.Portfolio.GetAssetBySecurityId(position.Security.QuoteSecurity.Id);

            _log.Info($"\n\tPOS: [{position.UpdateTime:HHmmss}][{position.SecurityCode}][Closed]\n\t\tID:{position.Id}, TID:{trade.Id}, PNL:{position.Notional:F4}, R:{position.Return:P4}, COUNT:{position.TradeCount}, Notional:{initialQuoteAsset?.Quantity}=>{currentQuoteAsset?.Quantity}");

            Algorithm.AfterPositionClosed(current);
        }
        else
        {
            var actualPrice = position.Side == Side.Buy ? $"LPRX:{position.LongPrice}" : $"SPRX:{position.ShortPrice}";
            if (!position.IsNew)
            {
                _log.Info($"\n\tPOS: [{position.UpdateTime:HHmmss}][{position.SecurityCode}][Updated]\n\t\tID:{position.Id}, TID:{trade.Id}, {actualPrice}, COUNT:{position.TradeCount}, QTY:{position.Quantity}");
                Algorithm.AfterPositionUpdated(current);
            }
            else
            {
                _log.Info($"\n\tPOS: [{position.UpdateTime:HHmmss}][{position.SecurityCode}][Opened]\n\t\tID:{position.Id}, TID:{trade.Id}, {actualPrice}, COUNT:{position.TradeCount}, QTY:{position.Quantity}");
                Algorithm.AfterPositionCreated(current);
            }

        }
    }

    public async Task Stop()
    {
        if (Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        _log.Info("Algorithm Engine is shutting down.");

        _runningState = AlgoRunningState.Stopped;
        _services.MarketData.NextOhlc -= OnNextOhlcPrice;
        _services.MarketData.NextTick -= OnNextTickPrice;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;

        _services.Order.OrderProcessed -= OnOrderProcessed;
        _services.Trade.TradeProcessed -= OnTradeProcessed;
        _services.Portfolio.PositionProcessed -= OnPositionProcessed;

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

    private async void OnNextTickPrice(int securityId, string securityCode, Tick tick)
    {
        await TryStopLoss(securityId, tick);
        await TryTakeProfit(securityId, tick);
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

        if (_pickedSecurities == null || !_pickedSecurities.TryGetValue(securityId, out var security))
            return;

        Algorithm.BeforeProcessingSecurity(security);

        var entries = _allEntriesBySecurityIds.GetOrCreate(security.Id);
        var last = _lastEntriesBySecurityId.ThreadSafeGet(security.Id);
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
        _currentEntriesBySecurityId.ThreadSafeSet(securityId, current);
        _services.Security.Fix(current, security);

        if (last == null)
        {
            last = current;
            _lastOhlcPricesBySecurityId.ThreadSafeSet(securityId, ohlcPrice);
            _lastEntriesBySecurityId.ThreadSafeSet(securityId, last);
            // current entry dict will be updated later
            entries.Add(current);
            return;
        }

        current.Return = (price - last.Price) / last.Price;

        // copy over most of the states from exitPrice to this
        if (last.LongCloseType == CloseType.None && last.ShortCloseType == CloseType.None)
        {
            CopyEntry(current, last, security, price);
        }

        if (IsBackTesting)
        {
            BackTestCheckLongStopLoss(current, last, security, ohlcPrice, _intervalType);
            BackTestCheckShortStopLoss(current, last, security, ohlcPrice, _intervalType);
        }

        var lastOhlcPrice = _lastOhlcPricesBySecurityId[securityId];
        Algorithm.Analyze(current, last, ohlcPrice, lastOhlcPrice);

        // determine whether to cancel partially filled or live orders (limit orders may live for a very long time)
        if (await TryCleanUpOpenOrders(current))
        {
            _log.Info($"\n\tORD: [{current.Time:HHmmss}][{current.SecurityCode}][{current.SequenceId}][CloseOpened]");
        }

        // try to open or close long or short
        if (await TryOpenLong(current, last, ohlcPrice, lastOhlcPrice))
        {
            LogAndSaveAlgoEntry(current, OrderActionType.AlgoOpen, Side.Buy);
        }
        else if (await TryCloseLong(current, ohlcPrice, _intervalType))
        {
            LogAndSaveAlgoEntry(current, OrderActionType.AlgoClose, Side.Sell);
        }
        else if (await TryOpenShort(current, last, ohlcPrice, lastOhlcPrice))
        {
            LogAndSaveAlgoEntry(current, OrderActionType.AlgoOpen, Side.Sell);
        }
        else if (await TryCloseShort(current, ohlcPrice, _intervalType))
        {
            LogAndSaveAlgoEntry(current, OrderActionType.AlgoClose, Side.Buy);
        }
        else
        {
            LogAndSaveAlgoEntry(current, null, null);
        }
        entries.Add(current);

        Algorithm.AfterProcessingSecurity(security);

        _lastEntriesBySecurityId.ThreadSafeSet(securityId, current);
        _lastOhlcPricesBySecurityId.ThreadSafeSet(securityId, ohlcPrice);
    }

    private void LogAndSaveAlgoEntry(AlgoEntry current, OrderActionType? actionType, Side? side)
    {
        if (actionType == null)
            _log.Info($"\n\tEVT: [{current.Time:HHmmss}][{current.SecurityCode}][{current.SequenceId}]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(current.Security)}}}");
        else
            _log.Info($"\n\tEVT: [{current.Time:HHmmss}][{current.SecurityCode}][{current.SequenceId}][{actionType}][{side}]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(current.Security)}}}");
    }

    private async Task ClosePositionIfNeeded()
    {
        if (_runningState is AlgoRunningState.Stopped or AlgoRunningState.Halted)
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

    /// <summary>
    /// Clean up existing alive open orders before any other actions.
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    private async Task<bool> TryCleanUpOpenOrders(AlgoEntry current)
    {
        if (Algorithm == null) throw Exceptions.InvalidAlgorithmEngineState();

        var openOrders = Algorithm.PickOpenOrdersToCleanUp(current);
        if (openOrders.IsNullOrEmpty())
            return false;

        var hasRealOrders = openOrders.Any(o => o.Type is OrderType.Limit or OrderType.Market);
        if (!hasRealOrders)
            return false;

        foreach (var openOrder in openOrders)
        {
            await _services.Order.CancelOrder(openOrder);
        }
        return true;
    }

    private async Task<bool> TryOpenLong(AlgoEntry current, AlgoEntry last, OhlcPrice price, OhlcPrice lastPrice)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        var security = current.Security;

        if (!Algorithm.CanOpenLong(current))
            return false;

        // cancel any partial filled, SL or TP orders
        await _services.Order.CancelAllOpenOrders(current.Security, OrderActionType.CleanUpLive, false);

        var time = DateTime.UtcNow;
        var enterSide = Side.Buy;

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
        {
            var sl = Algorithm.GetStopLossPrice(price.C, enterSide, security);
            var tp = Algorithm.GetTakeProfitPrice(price.C, enterSide, security);
            EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
        }
        else
        {
            await Algorithm.Open(current, last, price.C, enterSide, time);
        }

        current.SequenceId = _algoEntryIdGen.NewInt;
        _persistence.Insert(current);

        return true;
    }

    private async Task<bool> TryOpenShort(AlgoEntry current, AlgoEntry last, OhlcPrice price, OhlcPrice lastPrice)
    {
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        var security = current.Security;

        if (!Algorithm.CanOpenShort(current))
            return false;

        var time = DateTime.UtcNow;
        var enterSide = Side.Sell;
        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
        {
            var sl = Algorithm.GetStopLossPrice(price.C, enterSide, security);
            var tp = Algorithm.GetTakeProfitPrice(price.C, enterSide, security);
            EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
        }
        else
        {
            _log.Info($"\n\tEVT: [{current.Time:HHmmss}][{current.SecurityCode}][{current.SequenceId}][OpenShort]\n\t\tR:{current.Return:P4}, STATES:{{{current.Variables.Format(security)}}}");
            await Algorithm.Open(current, last, price.C, enterSide, time);
        }

        current.SequenceId = _algoEntryIdGen.NewInt;
        _persistence.Insert(current);

        return true;
    }

    private async Task<bool> TryCloseLong(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!Algorithm.CanCloseLong(current))
            return false;

        var security = current.Security;

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
            ExitLogic.BackTestClose(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
        else
            await Algorithm.Close(current, current.Security, Side.Sell, GetOhlcEndTime(ohlcPrice, intervalType), OrderActionType.AlgoClose);

        _persistence.Insert(current);

        return true;
    }

    private async Task<bool> TryCloseShort(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        var security = current.Security;
        if (!Algorithm.CanCloseShort(current))
            return false;

        _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(current);
        if (IsBackTesting)
            ExitLogic.BackTestClose(current, ohlcPrice.C, GetOhlcEndTime(ohlcPrice, intervalType));
        else
            await Algorithm.Close(current, current.Security, Side.Buy, GetOhlcEndTime(ohlcPrice, intervalType), OrderActionType.AlgoClose);

        _persistence.Insert(current);

        return true;
    }

    private async Task<bool> TryStopLoss(int securityId, Tick tick)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        if (!Algorithm.ShallStopLoss(securityId, tick))
            return false;

        var position = _services.Portfolio.GetPositionBySecurityId(securityId);
        if (position != null && !position.IsClosed)
        {
            var algoEntry = _currentEntriesBySecurityId.ThreadSafeGet(securityId);
            _log.Info($"\n\tEVT: [{DateTime.UtcNow:HHmmss}][{position.SecurityCode}][{algoEntry?.SequenceId}][StopLoss]\n\t\tR:{position.Return:P4}");

            var state = await Algorithm.CloseByTickStopLoss(position);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                Algorithm.AfterStoppedLoss(algoEntry!, position.CloseSide);
            }
            else
            {
                _log.Error($"STOP LOSS ORDER FAILURE! SecId: {securityId}");
            }
            return true;
        }
        return false;
    }

    private async Task TryTakeProfit(int securityId, Tick tick)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!Algorithm.ShallTakeProfit(securityId, tick))
            return;

        var position = _services.Portfolio.GetPositionBySecurityId(securityId);
        if (position != null && !position.IsClosed)
        {
            var algoEntry = _currentEntriesBySecurityId.ThreadSafeGet(securityId);
            _log.Info($"\n\tEVT: [{DateTime.UtcNow:HHmmss}][{position.SecurityCode}][{algoEntry?.SequenceId}][TakeProfit]\n\t\tR:{position.Return:P4}");

            var state = await Algorithm.CloseByTickTakeProfit(position);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                Algorithm.AfterTookProfit(algoEntry!, position.CloseSide);
            }
        }
    }

    private bool BackTestCheckLongStopLoss(AlgoEntry entry, AlgoEntry lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasLongPosition = _services.Portfolio.GetOpenPositionSide(entry.SecurityId) == Side.Buy;
        var stopLossPrice = Algorithm.GetStopLossPrice(entry.EnterPrice.Value, Side.Buy, entry.Security);
        if (IsBackTesting && hasLongPosition && ohlcPrice.L <= stopLossPrice)
        {
            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            return true;
        }
        return false;
    }

    private bool BackTestCheckShortStopLoss(AlgoEntry entry, AlgoEntry lastEntry, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        if (entry.EnterPrice == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasShortPosition = _services.Portfolio.GetOpenPositionSide(entry.SecurityId) == Side.Sell;
        var stopLossPrice = Algorithm.GetStopLossPrice(entry.EnterPrice.Value, Side.Sell, entry.Security);
        if (IsBackTesting && hasShortPosition && ohlcPrice.H >= stopLossPrice)
        {
            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(entry, lastEntry, GetOhlcEndTime(ohlcPrice, intervalType));
            _executionEntriesBySecurityIds.GetOrCreate(security.Id).Add(entry);
            return true;
        }
        return false;
    }

    private static DateTime GetOhlcEndTime(OhlcPrice price, IntervalType intervalType)
    {
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    protected void CopyEntry(AlgoEntry current, AlgoEntry last, Security security, decimal currentPrice)
    {
        current.SequenceId = last.SequenceId;
        var position = _services.Portfolio.GetPositionBySecurityId(current.SecurityId);
        if (position != null && !position.IsClosed)
        {
            current.Quantity = position.Quantity;
            current.EnterPrice = position.Side == Side.Buy ? position.LongPrice : position.ShortPrice;
            current.EnterTime = position.CreateTime;
            current.ExitPrice = position.Side == Side.Buy ? position.ShortPrice : position.LongPrice;
            current.Elapsed = position.UpdateTime - position.CreateTime;
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
