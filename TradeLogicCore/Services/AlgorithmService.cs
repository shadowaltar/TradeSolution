using Common;
using log4net;
using TradeCommon.Algorithms;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;

public class AlgorithmService : IAlgorithmService
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<long, AlgoEntry?> _lastEntriesBySecurityId = [];

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<long, AlgoEntry?> _currentEntriesBySecurityId = [];

    /// <summary>
    /// Caches last OHLC price. Key is securityId.
    /// </summary>
    private readonly Dictionary<long, OhlcPrice> _lastOhlcPricesBySecurityId = [];

    /// <summary>
    /// Caches full history of entries.
    /// </summary>
    private readonly Dictionary<long, List<AlgoEntry>> _allEntriesBySecurityIds = [];

    /// <summary>
    /// Caches entries related to execution only.
    /// </summary>
    private readonly Dictionary<long, List<AlgoEntry>> _executionEntriesBySecurityIds = [];

    private readonly Context _context;

    public AlgorithmService(Context context)
    {
        _context = context;
    }

    public AlgoSession? Session { get; private set; }

    AlgoSession IAlgorithmService.Session { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public List<AlgoEntry> GetAllEntries(long securityId)
    {
        return _allEntriesBySecurityIds.ThreadSafeGetOrCreate(securityId);
    }

    public List<long> GetAllSecurityIds()
    {
        return [.. _allEntriesBySecurityIds.Keys];
    }

    public AlgoEntry? GetCurrentEntry(long securityId)
    {
        return _currentEntriesBySecurityId.ThreadSafeGet(securityId);
    }

    public List<AlgoEntry> GetExecutionEntries(long securityId)
    {
        return _executionEntriesBySecurityIds.ThreadSafeGetOrCreate(securityId);
    }

    public AlgoEntry? GetLastEntry(long securityId)
    {
        return _lastEntriesBySecurityId.ThreadSafeGet(securityId);
    }

    public OhlcPrice? GetLastOhlcPrice(long securityId, IntervalType interval)
    {
        // TODO: currently only support one kind of interval type
        return _lastOhlcPricesBySecurityId.ThreadSafeGet(securityId);
    }

    public AlgoEntry? GetLastEntryAt(long securityId, int offset)
    {
        if (offset > 0) return null;
        var entries = _allEntriesBySecurityIds.ThreadSafeGet(securityId);
        if (entries.IsNullOrEmpty()) return null;
        if (entries.Count <= -offset) return null;
        lock (entries)
        {
            return entries[^offset];
        }
    }

    public AlgoEntry CreateCurrentEntry(Algorithm algorithm, Security security, OhlcPrice ohlcPrice, out AlgoEntry? last)
    {
        Session = Session ?? throw Exceptions.Impossible("Must have initialized with correct AlgoSession.");

        last = GetLastEntry(security.Id); // null when just started

        var current = new AlgoEntry
        {
            SessionId = Session.Id,
            SecurityId = security.Id,
            Security = security,
            PositionId = 0,
            Time = ohlcPrice.T,
            Variables = algorithm.CalculateVariables(ohlcPrice.C, last),
            Price = ohlcPrice.C,
        };
        _currentEntriesBySecurityId.ThreadSafeSet(security.Id, current);

        // if just started, return null last entry but set other caches' last entry = current entry
        if (last == null)
        {
            MoveNext(current, ohlcPrice);
        }

        return current;
    }

    public void MoveNext(AlgoEntry current, OhlcPrice ohlcPrice)
    {
        if (current.IsExecuting)
        {
            _executionEntriesBySecurityIds.ThreadSafeGetOrCreate(current.SecurityId).ThreadSafeAdd(current);
        }

        GetAllEntries(current.SecurityId).ThreadSafeAdd(current);

        _lastEntriesBySecurityId.ThreadSafeSet(current.SecurityId, current);
        _lastOhlcPricesBySecurityId.ThreadSafeSet(current.SecurityId, ohlcPrice);
    }

    //public void RecordExecution(AlgoEntry current)
    //{
    //    _executionEntriesBySecurityIds.GetOrCreate(current.SecurityId).Add(current);
    //}

    public void CopyOver(AlgoEntry current, AlgoEntry last, decimal price)
    {
        var asset = _context.Services.Algo.GetAsset(current);
        if (asset != null && !asset.IsEmpty)
        {
            current.Quantity = asset.Quantity;
            current.TheoreticEnterPrice = last.TheoreticEnterPrice;
            current.TheoreticEnterTime = last.TheoreticEnterTime;
            current.TheoreticExitPrice = last.TheoreticExitPrice;
            current.TheoreticExitTime = last.TheoreticExitTime;
            current.LongSignal = last.LongSignal;
            current.ShortSignal = last.ShortSignal;
            current.LongPrice = last.LongPrice;
            current.ShortPrice = last.ShortPrice;
            current.LongQuantity = last.LongQuantity;
            current.ShortQuantity = last.ShortQuantity;
            current.BaseFee = last.BaseFee;
            current.QuoteFee = last.QuoteFee;

            current.PositionId = last.PositionId;
        }
        else
        {
            current.LongSignal = SignalType.None;
            current.ShortSignal = SignalType.None;
            current.TheoreticEnterPrice = null;
            current.TheoreticEnterTime = null;
            current.TheoreticExitPrice = null;
            current.TheoreticExitTime = null;
            current.Quantity = 0;
            current.TheoreticEnterPrice = null;
            current.TheoreticEnterTime = null;
            current.TheoreticExitPrice = null;
            current.BaseFee = 0;
            current.QuoteFee = 0;

            current.PositionId = 0;
        }

        // if signal is hold, keep on holding
        if (last.LongSignal == SignalType.Open)
        {
            current.LongSignal = SignalType.None;
        }
        if (last.ShortSignal == SignalType.Open)
        {
            current.ShortSignal = SignalType.None;
        }

        current.LongCloseType = CloseType.None;
        current.ShortCloseType = CloseType.None;

        current.SequenceId = last.SequenceId + 1;
    }

    public void InitializeSession(EngineParameters engineParameters)
    {
        var _algorithm = _context.GetAlgorithm() ?? throw new InvalidOperationException("Must specify algorithm and algo-engine before saving an algo-session entry.");
        var uniqueId = IdGenerators.Get<AlgoSession>().NewTimeBasedId;

        Session = new AlgoSession
        {
            Id = uniqueId,
            AlgoId = _algorithm.Id,
            AlgoName = _algorithm.GetType().Name,
            AlgoVersionId = _algorithm.VersionId,
            UserId = _context.UserId,
            AccountId = _context.AccountId,
            Environment = _context.Environment,
            AlgorithmParameters = _algorithm.AlgorithmParameters,
            AlgorithmParametersInString = _algorithm.AlgorithmParameters.ToString() + _algorithm.PrintAlgorithmParameters(),
            EngineParameters = engineParameters,
            EngineParametersInString = engineParameters.ToString(),
            StartTime = DateTime.UtcNow,
        };

        _context.AlgoSession = Session;
    }

    public Asset? GetCash(AlgoEntry current)
    {
        if (current.Security.QuoteSecurity == null)
        {
            _log.Error("Failed to get correct quote security from security: " + current.Security.Code);
            return null;
        }
        return _context.Services.Portfolio.GetCashAssetBySecurityId(current.Security.QuoteSecurity.Id);
    }

    public Asset? GetAsset(AlgoEntry current)
    {
        if (current.Security.FxInfo?.BaseCurrency != null)
        {
            var baseSecurity = current.Security.FxInfo.BaseSecurity;
            return _context.Services.Portfolio.GetAssetBySecurityId(baseSecurity!.Id);
        }
        return _context.Services.Portfolio.GetAssetBySecurityId(current.SecurityId);
    }
}
