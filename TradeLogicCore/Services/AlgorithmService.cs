using Autofac.Core;
using Common;
using System.Diagnostics;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;

public class AlgorithmService : IAlgorithmService
{
    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry?> _lastEntriesBySecurityId = [];

    /// <summary>
    /// Caches algo-entries related to last time frame.
    /// Key is securityId.
    /// </summary>
    private readonly Dictionary<int, AlgoEntry?> _currentEntriesBySecurityId = [];

    /// <summary>
    /// Caches last OHLC price. Key is securityId.
    /// </summary>
    private readonly Dictionary<int, OhlcPrice> _lastOhlcPricesBySecurityId = [];

    /// <summary>
    /// Caches full history of entries.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry>> _allEntriesBySecurityIds = [];

    /// <summary>
    /// Caches entries related to execution only.
    /// </summary>
    private readonly Dictionary<int, List<AlgoEntry>> _executionEntriesBySecurityIds = [];

    public AlgoSession? Session { get; set; }

    public List<AlgoEntry> GetAllEntries(int securityId)
    {
        return _allEntriesBySecurityIds.ThreadSafeGetOrCreate(securityId);
    }

    public List<int> GetAllSecurityIds()
    {
        return _allEntriesBySecurityIds.Keys.ToList();
    }

    public AlgoEntry GetCurrentEntry(int securityId)
    {
        throw new NotImplementedException();
    }

    public List<AlgoEntry> GetExecutionEntries(int securityId)
    {
        return _executionEntriesBySecurityIds.ThreadSafeGetOrCreate(securityId);
    }

    public AlgoEntry? GetLastEntry(int securityId)
    {
        throw new NotImplementedException();
    }

    public OhlcPrice? GetLastEntry(int securityId, string interval)
    {
        throw new NotImplementedException();
    }

    public AlgoEntry? GetLastEntryAt(int securityId, int offset)
    {
        throw new NotImplementedException();
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
            _lastOhlcPricesBySecurityId.ThreadSafeSet(security.Id, ohlcPrice);
            _lastEntriesBySecurityId.ThreadSafeSet(security.Id, current);
            // current entry dict will be updated later
            var allEntries = _allEntriesBySecurityIds.ThreadSafeGetOrCreate(security.Id);
            allEntries.ThreadSafeAdd(current);
        }

        return current;
    }

    public void Next()
    {
        throw new NotImplementedException();
    }

    List<long> IAlgorithmService.GetAllSecurityIds()
    {
        throw new NotImplementedException();
    }
}
