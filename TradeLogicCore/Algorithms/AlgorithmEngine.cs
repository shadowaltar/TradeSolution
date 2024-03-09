using Common;
using log4net;
using OfficeOpenXml.Style;
using System.Diagnostics;
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

/// <summary>
/// One algo engine is able to handle:
/// * one algorithm.
/// * one OHLC time interval right now (plan to make it able to handle multiple intervals).
/// * multiple securities.
/// </summary>
public class AlgorithmEngine : IAlgorithmEngine
{
    private static readonly ILog _log = Logger.New();

    private readonly Context _context;
    private readonly IServices _services;
    private readonly Persistence _persistence;
    private readonly AutoResetEvent _signal = new(false);

    private readonly int _engineThreadId;

    private readonly IntervalType _intervalType;
    private readonly List<ExtendedOrderBook> _orderBookSavingBuffer = [];
    private IReadOnlyDictionary<int, Security>? _pickedSecurities;

    private readonly bool _isRecordingOrderBook = false;

    /// <summary>
    /// Caches order books.
    /// </summary>
    private readonly Dictionary<int, OrderBookCache> _orderBookCaches = [];

    private readonly IdGenerator _algoEntryIdGen;

    private AlgoRunningState _runningState = AlgoRunningState.NotYetStarted;

    public event Action? ReachedDesignatedEndTime;

    public long SessionId => AlgoSession.Id;

    public bool IsBackTesting { get; private set; } = true;

    public EngineParameters EngineParameters { get; private set; }

    public IPositionSizingAlgoLogic? Sizing { get; protected set; }

    public IEnterPositionAlgoLogic? EnterLogic { get; protected set; }

    public IExitPositionAlgoLogic? ExitLogic { get; protected set; }

    public ISecurityScreeningAlgoLogic? Screening { get; protected set; }

    public int TotalPriceEventCount { get; protected set; }
    public int TotalTickEventCount { get; protected set; }
    public int TotalOrderBookEventCount { get; protected set; }

    public AlgoSession AlgoSession { get; private set; }

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

    public AlgorithmEngine(Context context, Algorithm algorithm, EngineParameters engineParameters)
    {
        _context = context;
        _services = context.Services;
        _persistence = _services.Persistence;
        EngineParameters = engineParameters;

        _context.SetPreferredQuoteCurrencies(EngineParameters.PreferredQuoteCurrencies);
        _context.SetSecurityCodeWhiteList(EngineParameters.GlobalCurrencyFilter);
        _context.InitializeAlgorithmContext(this, algorithm);

        _services.Algo.InitializeSession(EngineParameters);
        AlgoSession = _context.AlgoSession!;

        _algoEntryIdGen = IdGenerators.Get<AlgoEntry>();
        _engineThreadId = Environment.CurrentManagedThreadId;

        _services.Order.OrderProcessed += OnOrderProcessed;
        _services.Trade.TradeProcessed += OnTradeProcessed;
        _services.Portfolio.AssetProcessed += OnAssetProcessed;

        Algorithm = algorithm;
        Sizing = algorithm.Sizing;
        EnterLogic = algorithm.Entering;
        ExitLogic = algorithm.Exiting;
        Screening = algorithm.Screening;
    }

    public List<AlgoEntry> GetAllEntries(int securityId) => _services.Algo.GetAllEntries(securityId);

    public List<AlgoEntry> GetExecutionEntries(int securityId) => _services.Algo.GetExecutionEntries(securityId);

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

        await InitializeCaches();

        // close open positions
        if (EngineParameters.CloseOpenPositionsOnStart)
        {
            if (await _services.Portfolio.CloseAllPositions(Comments.CloseAllBeforeStart))
            {
                await InitializeCaches(); // reload cache
            }
        }

        // subscribe to events
        _services.MarketData.NextOhlc += OnNextOhlcPrice;
        _services.MarketData.NextTick += OnNextTickPrice;
        _services.MarketData.NextOrderBook += OnNextOrderBook;
        _services.MarketData.HistoricalPriceEnd += OnHistoricalPriceEnd;

        // subscribe to prices
        var subscriptionCount = 0;
        var pickedSecurities = Screening.GetAll();
        foreach (var security in pickedSecurities.Values)
        {
            var assetPosition = _services.Portfolio.GetRelatedCashPosition(security);
            if (assetPosition == null || assetPosition.Quantity <= 0)
            {
                _log.Warn($"Cannot trade the picked security {security.Code}; the account may not have enough free asset to trade.");
                continue;
            }
            await _services.MarketData.PrepareOrderBookTable(security, Consts.OrderBookLevels);
            await _services.MarketData.SubscribeOhlc(security, Interval);
            await _services.MarketData.SubscribeOrderBook(security, Consts.OrderBookLevels);
            if (AlgoParameters.RequiresTickData || AlgoParameters.StopOrderTriggerBy == StopOrderStyleType.TickSignal)
                await _services.MarketData.SubscribeTick(security);
            subscriptionCount++;
        }
        SetAlgoEffectiveTimeRange(parameters.TimeRange);

        // wait for the price thread to be stopped by unsubscription or forceful algo exit
        _signal.WaitOne();
        _runningState = AlgoRunningState.Stopped;
        _log.Info("Algorithm Engine execution ends, processed " + TotalPriceEventCount);
        return TotalPriceEventCount;
    }

    private async Task InitializeCaches()
    {
        _services.Order.Reset();
        _services.Trade.Reset();
        await _services.Portfolio.Reload(false, true);
    }

    private void OnOrderProcessed(Order order)
    {
        var filledQtyStr = order.Status == OrderStatus.PartialFilled ? ", FILLQTY:" + order.FormattedFilledQuantity : "";
        var orderTypeStr = "";
        string orderPriceStr;
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

        var current = _services.Algo.GetCurrentEntry(order.SecurityId);
        if (current != null)
        {
            current.OrderCount++;
            if (order.AlgoEntryId == 0)
            {
                order.AlgoEntryId = current.SequenceId;
                order.AlgoSessionId = current.SessionId;
            }
        }
        else
        {
            _log.Debug("Received an order not from algo execution but manual or auto position closing logic.");
        }
        _log.Info($"\n\tORD: [{order.UpdateTime:HHmmss}][{order.SecurityCode}][{order.Type}][{order.Action}][{order.Status}][{order.Side}]\n\t\tID:{order.Id}, {orderTypeStr}P*Q:{orderPriceStr}*{order.FormattedQuantity}{filledQtyStr}");
    }

    private void OnTradeProcessed(Trade trade)
    {
        var current = _services.Algo.GetCurrentEntry(trade.SecurityId);
        if (current != null)
        {
            current.TradeCount++;
            if (trade.AlgoEntryId == 0)
            {
                trade.AlgoEntryId = current.SequenceId;
                trade.AlgoSessionId = current.SessionId;
            }
        }
        else
        {
            _log.Debug("Received a trade not from algo execution but manual or auto position closing logic.");
        }
        var order = _services.Order.GetOrderByExternalId(trade.ExternalOrderId);
        if (order != null)
        {

        }

        _log.Info($"\n\tTRD: [{trade.Time:HHmmss}][{trade.SecurityCode}][{trade.Side}]\n\t\tID:{trade.Id}, P*Q:{trade.Price}*{trade.Quantity}");
    }

    private void OnAssetProcessed(Asset asset, Trade trade)
    {
        var current = _services.Algo.GetCurrentEntry(asset.SecurityId);
        if (current == null) throw new InvalidOperationException("Must initialize first.");

        if (_runningState == AlgoRunningState.NotYetStarted)
            return;

        if (asset.IsClosed)
        {
            var quoteSecurityId = asset.Security.QuoteSecurity?.Id ?? throw Exceptions.Impossible("A position must has a quote currency.");
            var initCashPosition = _services.Portfolio.InitialPortfolio.GetCashAssetBySecurityId(quoteSecurityId);
            var currentCashPosition = _services.Portfolio.Portfolio.GetCashAssetBySecurityId(quoteSecurityId);

            _log.Info($"\n\tPOS: [{asset.UpdateTime:HHmmss}][{asset.SecurityCode}][Closed]\n\t\tID:{asset.Id}, TID:{trade.Id}, PNL:{current.Notional:F4}, R:{current.EntryReturn:P4}, Notional:{initCashPosition?.Quantity}=>{currentCashPosition?.Quantity}");

            StopOrderBookRecording(current.SecurityId);
        }
        else
        {
            _log.Info($"\n\tPOS: [{asset.UpdateTime:HHmmss}][{asset.SecurityCode}][Updated]\n\t\tID:{asset.Id}, TID:{trade.Id}, COUNT:{current.TradeCount}, P*Q:{trade.Price}*{asset.Quantity}");
        }
        Algorithm.AfterPositionChanged(current);
    }

    public async Task Stop()
    {
        if (Screening == null) throw Exceptions.InvalidAlgorithmEngineState();

        _log.Info("Algorithm Engine is shutting down.");

        _runningState = AlgoRunningState.Stopped;
        _services.MarketData.NextOhlc -= OnNextOhlcPrice;
        _services.MarketData.NextTick -= OnNextTickPrice;
        _services.MarketData.NextOrderBook -= OnNextOrderBook;
        _services.MarketData.HistoricalPriceEnd -= OnHistoricalPriceEnd;

        _services.Order.OrderProcessed -= OnOrderProcessed;
        _services.Trade.TradeProcessed -= OnTradeProcessed;
        _services.Portfolio.AssetProcessed -= OnAssetProcessed;

        await _services.MarketData.UnsubscribeAllOhlcs();
        var securities = Screening.GetAll();
        foreach (var security in securities.Values)
        {
            await _services.MarketData.UnsubscribeOhlc(security, Interval);
            await _services.MarketData.UnsubscribeTick(security);
            await _services.MarketData.UnsubscribeOrderBook(security);
        }
        // unblock the main thread and let it finish
        _signal.Set();
        // set algo end time
        AlgoSession.EndTime = DateTime.UtcNow;
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

    private async void OnNextTickPrice(int securityId, string securityCode, Tick tick)
    {
        if (_runningState != AlgoRunningState.Running)
            return;

        var current = _services.Algo.GetCurrentEntry(securityId);
        if (current == null)
            return;

        TotalTickEventCount++;
        if (AlgoParameters!.StopOrderTriggerBy == StopOrderStyleType.TickSignal)
        {
            await TryStopLoss(current, securityId, tick);
            await TryTakeProfit(current, securityId, tick);
        }
    }

    private void OnNextOrderBook(ExtendedOrderBook orderBook)
    {
        if (_runningState != AlgoRunningState.Running)
            return;
        if (!EngineParameters.RecordOrderBookOnExecution)
            return;
        try
        {
            var cache = _orderBookCaches.ThreadSafeGet(orderBook.SecurityId, () => new OrderBookCache(_context.Storage, orderBook.SecurityId));
            cache.Add(orderBook);
        }
        catch (Exception e)
        {
            _log.Error("Failed to prepare or add order book into cache for security id: " + orderBook.SecurityId, e);
        }
    }

    /// <summary>
    /// Handler which is invoked when price feed notifies a new price object arrives.
    /// We expect this is a separated thread from the original engine.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="price"></param>
    /// <param name="interval"></param>
    /// <param name="isComplete"></param>
    private async void OnNextOhlcPrice(int securityId, OhlcPrice price, IntervalType interval, bool isComplete)
    {
        if (Screening == null) throw Exceptions.Impossible("Screening logic must have been initialized.");

        await ClosePositionIfNeeded();

        if (!CanAcceptPrice())
            return;

        if (!isComplete)
            return;

        if (Screening.TryCheckIfChanged(out var securities) || _pickedSecurities == null)
            _pickedSecurities = securities;

        var security = _pickedSecurities?.GetOrDefault(securityId) ?? throw Exceptions.MissingSecurity();
        _log.Info($"\n\tPRX: [{price.T:HHmmss}][{security.Code}]\n\t\tH:{security.RoundTickSize(price.H)}, L:{security.RoundTickSize(price.L)}, C:{security.RoundTickSize(price.C)}, V:{price.V}");

        TotalPriceEventCount++;
        await Update(securityId, price);
    }

    /// <summary>
    /// Main logic when an OHLC price is received.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="ohlcPrice"></param>
    public async Task Update(int securityId, OhlcPrice ohlcPrice)
    {
        if (Algorithm == null || AlgoParameters == null || Screening == null || AlgoSession == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (_pickedSecurities == null || !_pickedSecurities.TryGetValue(securityId, out var security))
            return;

        Algorithm.BeforeProcessingSecurity(security);

        // TODO need testing
        //_persistence.InsertPrice(security, ohlcPrice, AlgoParameters.Interval);

        var price = ohlcPrice.C;

        var current = _services.Algo.CreateCurrentEntry(Algorithm, security, ohlcPrice, out var last);

        _services.Security.Fix(current, security);

        if (last == null)
        {
            return;
        }

        current.EntryReturn = (price - last.Price) / last.Price;

        // copy over most of the states from exitPrice to this
        if (last.LongCloseType == CloseType.None && last.ShortCloseType == CloseType.None)
        {
            _services.Algo.CopyOver(current, last, price);
        }

        if (IsBackTesting)
        {
            BackTestCheckLongStopLoss(current, last, security, ohlcPrice, _intervalType);
            BackTestCheckShortStopLoss(current, last, security, ohlcPrice, _intervalType);
        }

        var lastOhlcPrice = _services.Algo.GetLastOhlcPrice(securityId, _intervalType);
        Algorithm.Analyze(current, last, ohlcPrice, lastOhlcPrice);

        // determine whether to cancel partially filled or live orders (limit orders may live for a very long time)
        if (await TryCleanUpOpenOrders(current))
        {
            _log.Info($"\n\tORD: [{current.Time:HHmmss}][{current.SecurityCode}][{current.PositionId}][CloseOpened]");
        }

        // try to open or close long or short
        var actionType = OrderActionType.Unknown;
        var actionSide = Side.None;
        foreach (var side in Sides.BuySell)
        {
            if (actionType != OrderActionType.Unknown)
                break;
            actionSide = side;
            if (await TryOpen(side, current, last, ohlcPrice))
            {
                actionType = OrderActionType.AlgoOpen;
            }
            else if (await TryClose(side, current, ohlcPrice, _intervalType))
            {
                actionType = OrderActionType.AlgoClose;
            }
        }
        if (actionType == OrderActionType.Unknown)
            LogEntry(current, null, null);
        else
            LogEntry(current, actionType, actionSide);

        _persistence.Insert(current);

        Algorithm.AfterProcessingSecurity(security);

        _services.Algo.MoveNext(current, ohlcPrice);
    }

    private static void LogEntry(AlgoEntry current, OrderActionType? actionType, Side? side)
    {
        if (actionType == null)
            _log.Info($"ALGO ENTRY: [{current.Time:HHmmss}][{current.SecurityCode}][{current.PositionId}]\n\t\tR:{current.EntryReturn:P4}, STATES:{{{current.Variables.Format(current.Security)}}}");
        else
            _log.Info($"ALGO [{actionType}/{side}]: [{current.Time:HHmmss}][{current.SecurityCode}][{current.PositionId}]\n\t\tR:{current.EntryReturn:P4}, STATES:{{{current.Variables.Format(current.Security)}}}");
    }

    private async Task ClosePositionIfNeeded()
    {
        if (_runningState is AlgoRunningState.Stopped or AlgoRunningState.Halted)
        {
            if (EngineParameters.CloseOpenPositionsOnStop && _services.Portfolio.HasAssetPosition)
            {
                await _services.Portfolio.CloseAllPositions(Comments.CloseAllBeforeStop);
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
        if (current == null) return false; // not yet started
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

    private async Task<bool> TryOpen(Side side, AlgoEntry current, AlgoEntry last, OhlcPrice price)
    {
        if (current == null) return false; // not yet started
        if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        var security = current.Security;

        if (!Algorithm.CanOpen(current, side))
            return false;

        var enterPrice = price.C;

        StartOrderBookRecording(current.SecurityId);

        // cancel any partial filled, SL or TP orders
        await _services.Order.CancelAllOpenOrders(current.Security, OrderActionType.CleanUpLive, false);

        var time = DateTime.UtcNow;
        current.IsExecuting = true;
        if (IsBackTesting)
        {
            var sl = Algorithm.GetStopLossPrice(enterPrice, side, security);
            var tp = Algorithm.GetTakeProfitPrice(enterPrice, side, security);
            EnterLogic.BackTestOpen(current, last, enterPrice, side, time, sl, tp);
        }
        else
        {
            var state = await Algorithm.Open(current, last, enterPrice, side, time);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                current.TheoreticEnterTime = time;
                current.TheoreticEnterPrice = enterPrice;
            }
        }
        current.PositionId = _algoEntryIdGen.NewInt;
        return true;
    }

    private async Task<bool> TryClose(Side side, AlgoEntry current, OhlcPrice price, IntervalType intervalType)
    {
        if (current == null) return false; // not yet started
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!Algorithm.CanClose(current, side))
            return false;

        current.IsExecuting = true;
        var endTime = GetOhlcEndTime(price, intervalType);
        var exitPrice = price.C;
        if (IsBackTesting)
        {
            ExitLogic.BackTestClose(current, exitPrice, endTime);
        }
        else
        {
            var state = await Algorithm.Close(current, current.Security, exitPrice, side.Invert(), endTime, OrderActionType.AlgoClose);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                current.TheoreticExitTime = endTime;
                current.TheoreticExitPrice = exitPrice;
            }
        }

        return true;
    }

    //private async Task<bool> TryOpenLong(AlgoEntry current, AlgoEntry last, OhlcPrice price)
    //{
    //    if (current == null) return false; // not yet started
    //    if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
    //    var security = current.Security;

    //    if (!Algorithm.CanOpen(current, Side.Buy))
    //        return false;

    //    StartOrderBookRecording(current.SecurityId);

    //    // cancel any partial filled, SL or TP orders
    //    await _services.Order.CancelAllOpenOrders(current.Security, OrderActionType.CleanUpLive, false);

    //    var time = DateTime.UtcNow;
    //    var enterSide = Side.Buy;

    //    _services.Algo.RecordExecution(current);
    //    if (IsBackTesting)
    //    {
    //        var sl = Algorithm.GetStopLossPrice(price.C, enterSide, security);
    //        var tp = Algorithm.GetTakeProfitPrice(price.C, enterSide, security);
    //        EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
    //    }
    //    else
    //    {
    //        await Algorithm.Open(current, last, price.C, enterSide, time);
    //    }

    //    current.PositionId = _algoEntryIdGen.NewInt;
    //    return true;
    //}

    //private async Task<bool> TryOpenShort(AlgoEntry current, AlgoEntry last, OhlcPrice price)
    //{
    //    if (current == null) return false; // not yet started
    //    if (Algorithm == null || EnterLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
    //    var security = current.Security;

    //    if (!Algorithm.CanOpen(current, Side.Sell))
    //        return false;

    //    StartOrderBookRecording(current.SecurityId);

    //    // cancel any partial filled, SL or TP orders
    //    await _services.Order.CancelAllOpenOrders(current.Security, OrderActionType.CleanUpLive, false);

    //    var time = DateTime.UtcNow;
    //    var enterSide = Side.Sell;
    //    _services.Algo.RecordExecution(current);
    //    if (IsBackTesting)
    //    {
    //        var sl = Algorithm.GetStopLossPrice(price.C, enterSide, security);
    //        var tp = Algorithm.GetTakeProfitPrice(price.C, enterSide, security);
    //        EnterLogic.BackTestOpen(current, last, price.C, enterSide, time, sl, tp);
    //    }
    //    else
    //    {
    //        _log.Info($"\n\tEVT: [{current.Time:HHmmss}][{current.SecurityCode}][{current.PositionId}][OpenShort]\n\t\tR:{current.EntryReturn:P4}, STATES:{{{current.Variables.Format(security)}}}");
    //        await Algorithm.Open(current, last, price.C, enterSide, time);
    //    }

    //    current.PositionId = _algoEntryIdGen.NewInt;
    //    return true;
    //}
    //private async Task<bool> TryCloseLong(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    //{
    //    if (current == null) return false; // not yet started
    //    if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

    //    if (!Algorithm.CanCloseLong(current))
    //        return false;

    //    current.IsExecuting = true;
    //    var endTime = GetOhlcEndTime(ohlcPrice, intervalType);
    //    if (IsBackTesting)
    //        ExitLogic.BackTestClose(current, ohlcPrice.C, endTime);
    //    else
    //        await Algorithm.Close(current, current.Security, ohlcPrice.C, Side.Sell, endTime, OrderActionType.AlgoClose);

    //    return true;
    //}

    //private async Task<bool> TryCloseShort(AlgoEntry current, OhlcPrice ohlcPrice, IntervalType intervalType)
    //{
    //    if (current == null) return false; // not yet started
    //    if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

    //    if (!Algorithm.CanCloseShort(current))
    //        return false;

    //    current.IsExecuting = true;
    //    var endTime = GetOhlcEndTime(ohlcPrice, intervalType);
    //    if (IsBackTesting)
    //        ExitLogic.BackTestClose(current, ohlcPrice.C, endTime);
    //    else
    //        await Algorithm.Close(current, current.Security, ohlcPrice.C, Side.Buy, endTime, OrderActionType.AlgoClose);

    //    return true;
    //}

    private async Task<bool> TryStopLoss(AlgoEntry current, int securityId, Tick tick)
    {
        if (current == null) return false; // not yet started
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        if (!Algorithm.ShallStopLoss(current, tick, out var triggerPrice))
            return false;

        var asset = _context.Services.Algo.GetAsset(current);
        if (asset != null && !asset.IsEmpty)
        {
            _log.Info($"\n\tEVT: [{DateTime.UtcNow:HHmmss}][{asset.SecurityCode}][{current?.PositionId}][StopLoss]\n\t\tR:{current.EntryReturn:P4}");
            _persistence.Insert(current);
            var state = await Algorithm.CloseByTickStopLoss(current, asset, triggerPrice);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                Algorithm.AfterStoppedLoss(current!);
            }
            else
            {
                _log.Error($"STOP LOSS ORDER FAILURE! SecId: {securityId}");
            }
            return true;
        }
        return false;
    }

    private async Task<bool> TryTakeProfit(AlgoEntry current, int securityId, Tick tick)
    {
        if (current == null) return false; // not yet started
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        if (!Algorithm.ShallTakeProfit(current, tick, out var triggerPrice))
            return false;

        var asset = _context.Services.Algo.GetAsset(current);
        if (asset != null && !asset.IsEmpty)
        {
            _log.Info($"\n\tEVT: [{DateTime.UtcNow:HHmmss}][{asset.SecurityCode}][{current?.PositionId}][TakeProfit]\n\t\tR:{current.EntryReturn:P4}");
            _persistence.Insert(current);
            var state = await Algorithm.CloseByTickTakeProfit(current, asset, triggerPrice);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                Algorithm.AfterTookProfit(current);
            }
            else
            {
                _log.Error($"TAKE PROFIT ORDER FAILURE! SecId: {securityId}");
            }
            return true;
        }
        return false;
    }

    private bool BackTestCheckLongStopLoss(AlgoEntry current, AlgoEntry last, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasLongPosition = _services.Portfolio.GetOpenPositionSide(current.SecurityId) == Side.Buy;
        var stopLossPrice = Algorithm.GetStopLossPrice(current.TheoreticEnterPrice.Value, Side.Buy, current.Security);
        if (IsBackTesting && hasLongPosition && ohlcPrice.L <= stopLossPrice)
        {
            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(current, last, GetOhlcEndTime(ohlcPrice, intervalType));
            current.IsExecuting = true;
            return true;
        }
        return false;
    }

    private bool BackTestCheckShortStopLoss(AlgoEntry current, AlgoEntry last, Security security, OhlcPrice ohlcPrice, IntervalType intervalType)
    {
        if (Algorithm == null || ExitLogic == null) throw Exceptions.InvalidAlgorithmEngineState();
        if (current.TheoreticEnterPrice == null) throw Exceptions.InvalidAlgorithmEngineState();

        var hasShortPosition = _services.Portfolio.GetOpenPositionSide(current.SecurityId) == Side.Sell;
        var stopLossPrice = Algorithm.GetStopLossPrice(current.TheoreticEnterPrice.Value, Side.Sell, current.Security);
        if (IsBackTesting && hasShortPosition && ohlcPrice.H >= stopLossPrice)
        {
            // assuming always stopped loss at the stopLossPrice
            ExitLogic.BackTestStopLoss(current, last, GetOhlcEndTime(ohlcPrice, intervalType));
            current.IsExecuting = true;
            return true;
        }
        return false;
    }

    private static DateTime GetOhlcEndTime(OhlcPrice price, IntervalType intervalType)
    {
        return price.T + IntervalTypeConverter.ToTimeSpan(intervalType);
    }

    //protected void CopyEntry(AlgoEntry current, AlgoEntry last, decimal currentPrice)
    //{
    //    current.SequenceId = last.SequenceId;
    //    var position = _services.Portfolio.GetAssetBySecurityId(current.SecurityId);
    //    if (position != null && !position.IsEmpty)
    //    {
    //        var side = _services.Portfolio.GetOpenPositionSide(current.SecurityId);
    //        current.Quantity = position.Quantity;
    //        current.EnterPrice = last.EnterPrice;
    //        current.EnterTime = position.CreateTime;
    //        current.ExitPrice = last.ExitPrice;
    //        current.Elapsed = position.UpdateTime - position.CreateTime;
    //        current.Fee = last.Fee;
    //    }
    //    else
    //    {
    //        current.LongSignal = SignalType.None;
    //        current.ShortSignal = SignalType.None;
    //        current.Quantity = 0;
    //        current.EnterPrice = null;
    //        current.EnterTime = null;
    //        current.ExitPrice = null;
    //        current.Elapsed = null;
    //        current.Fee = 0;
    //    }

    //    if (last.LongSignal == SignalType.Open)
    //    {
    //        current.LongSignal = SignalType.None;
    //    }
    //    if (last.ShortSignal == SignalType.Open)
    //    {
    //        current.ShortSignal = SignalType.None;
    //    }

    //    current.RealizedPnl = 0;
    //    current.LongCloseType = CloseType.None;
    //    current.ShortCloseType = CloseType.None;
    //    current.Notional = current.Quantity * currentPrice;
    //}

    private void StartOrderBookRecording(int securityId)
    {
        var cache = _orderBookCaches.ThreadSafeGet(securityId);
        if (cache == null)
            return;
        cache.StartPersistence();
    }

    private void StopOrderBookRecording(int securityId)
    {
        // we want to record some order books after it is stopped
        var cache = _orderBookCaches.ThreadSafeGet(securityId);
        if (cache == null)
            return;

        cache.StopPersistenceAfterAnotherFlush();
    }
}
