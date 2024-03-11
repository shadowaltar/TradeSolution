using Common;
using log4net;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;

namespace TradeDataCore.Instruments;
public class SecurityService(IStorage storage, ApplicationContext context) : ISecurityService
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<long, Security> _securities = [];
    private readonly Dictionary<string, Security> _securitiesByCode = [];
    private readonly Dictionary<(string code, ExchangeType exchange), long> _mapping = [];
    private readonly IStorage _storage = storage;
    private readonly ApplicationContext _context = context;

    public bool IsInitialized { get; private set; }

    public void Reset()
    {
        lock (_securities)
        {
            _securities.Clear();
            _securitiesByCode.Clear();
            _mapping.Clear();
        }
        IsInitialized = false;
    }

    public async Task Initialize()
    {
        if (IsInitialized) return;
        await ReadStorageAndRefreshCache();
        IsInitialized = true;

        // read cash assets config file
        var codes = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "CashAssets.txt"))?.ToList();
        if (codes.IsNullOrEmpty()) throw new InvalidOperationException("Failed to define cash asset codes.");

        foreach (var code in codes)
        {
            var security = GetSecurity(code);
            if (security == null)
            {
                _log.Error($"Security code {code} is not found! Cannot set it as cash currency. This code will be ignored.");
            }
            else
            {
                security.IsCash = true;
            }
        }
    }

    public async Task<List<Security>> GetAllSecurities(bool readStorage = false)
    {
        var isJustInitialized = false;
        if (!IsInitialized)
        {
            await Initialize();
            isJustInitialized = true;
        }

        if (isJustInitialized || !readStorage)
        {
            return _securities.ThreadSafeValues();
        }
        return await ReadStorageAndRefreshCache();
    }

    public async Task<List<Security>> GetSecurities(SecurityType secType,
                                                    ExchangeType exchange,
                                                    bool requestDatabase = false)
    {
        if (!IsInitialized)
            await Initialize();
        exchange = exchange == ExchangeType.Unknown ? _context.Exchange : exchange;
        if (requestDatabase)
        {
            var securities = await _storage.ReadSecurities(secType, exchange);
            RefreshCache(securities);
            return securities;
        }
        else
        {
            lock (_securities)
            {
                return _securities.Values.Where(s => s.ExchangeType == exchange).ToList();
            }
        }
    }

    public List<Security> GetAssets(ExchangeType exchange = ExchangeType.Unknown)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        if (exchange == ExchangeType.Unknown)
            exchange = _context.Exchange;
        var exchStr = ExchangeTypeConverter.ToString(exchange);
        lock (_securities)
        {
            return _securities.Values
                .Where(s => s.Exchange == exchStr && SecurityTypeConverter.Matches(s.Type, SecurityType.Fx) && s.FxInfo?.IsAsset == true)
                .ToList();
        }
    }

    public async Task<List<Security>> GetSecurities(List<long> securityIds, bool requestExternal = false)
    {
        if (!IsInitialized)
            await Initialize();
        if (requestExternal)
        {
            var securities = await _storage.ReadSecurities(securityIds);
            RefreshCache(securities);
            return securities;
        }
        else
        {
            lock (_securities)
            {
                return _securities
                    .Where(p => securityIds.Contains(p.Key))
                    .Select(p => p.Value).ToList();
            }
        }
    }

    public async Task<Security?> GetSecurity(string code, string exchange, bool requestExternal = false)
    {
        if (!IsInitialized)
            await Initialize();
        var exchangeType = ExchangeTypeConverter.Parse(exchange);
        return await GetSecurity(code, exchangeType, SecurityType.Unknown, requestExternal);
    }

    public async Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType = SecurityType.Unknown, bool requestExternal = false)
    {
        if (!IsInitialized)
            await Initialize();
        if (requestExternal)
        {
            var security = await _storage.ReadSecurity(exchange, code, securityType);
            return security;
        }
        else
        {
            lock (_securities)
            {
                var id = _mapping.TryGetValue((code, exchange), out var temp) ? temp : 0;
                return id <= 0 ? null : _securities.GetValueOrDefault(id);
            }
        }
    }

    public async Task<Security?> GetSecurity(long securityId, bool requestExternal = false)
    {
        if (!IsInitialized)
            await Initialize();
        if (requestExternal)
        {
            var securities = await _storage.ReadSecurities([securityId]);
            if (!securities.IsNullOrEmpty())
            {
                RefreshCache(securities[0]);
                return securities[0];
            }
            return null;
        }
        else
        {
            return GetSecurity(securityId);
        }
    }

    public async Task<Security?> GetSecurity(string code, SecurityType securityType)
    {
        if (!IsInitialized)
            await Initialize();
        return await GetSecurity(code, _context.Exchange, securityType, false);
    }

    public Security? GetSecurity(string? code)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        return code.IsBlank() ? null : _securitiesByCode.ThreadSafeGet(code);
    }

    public Security? GetFxSecurity(string baseCurrency, string quoteCurrency)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        if (baseCurrency.IsBlank() || quoteCurrency.IsBlank()) return null;
        return _securities.ThreadSafeFirst(s => s.Value.FxInfo?.BaseCurrency == baseCurrency && s.Value.FxInfo?.QuoteCurrency == quoteCurrency).Value;
    }

    public Security GetSecurity(long securityId)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        return _securities.ThreadSafeGet(securityId) ?? throw Exceptions.InvalidSecurityId(securityId);
    }

    public void Fix(SecurityRelatedEntry entry, Security? security = null)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        if (entry.SecurityId > 0)
        {
            security ??= GetSecurity(entry.SecurityId);
            if (security == null)
            {
                throw Exceptions.InvalidSecurityId(entry.SecurityId);
            }
            entry.SecurityCode = security.Code;
            entry.Security = security;
        }
        else if (!entry.SecurityCode.IsBlank())
        {
            security ??= GetSecurity(entry.SecurityCode);
            if (security == null)
            {
                throw Exceptions.InvalidSecurityCode(entry.SecurityCode);
            }
            entry.SecurityId = security.Id;
            entry.Security = security;
        }
        else if (entry.Security != null) // rare case, optional security comes first
        {
            security ??= entry.Security;
            entry.SecurityCode = security.Code;
            entry.SecurityId = security.Id;
            entry.Security = security;
        }

        if (entry.SecurityCode.IsBlank() || entry.SecurityId <= 0 || entry.Security == null)
        {
            throw Exceptions.InvalidSecurity("N/A", "no security information at all in this entry.");
        }
    }

    public void Fix<T>(IList<T> entries, Security? security = null) where T : SecurityRelatedEntry
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        foreach (var entry in entries)
        {
            Fix(entry, security);
        }
    }

    public async Task<(long securityId, int count)> InsertPrices(long securityId, IntervalType interval, SecurityType secType, List<OhlcPrice> prices)
    {
        return await _storage.InsertPrices(securityId, interval, secType, prices);
    }

    public async Task<List<OhlcPrice>> ReadPrices(long securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
    {
        return await _storage.ReadPrices(securityId, interval, securityType, start, end, priceDecimalPoints);
    }

    public async Task<Dictionary<long, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range)
    {
        return await _storage.ReadAllPrices(securities, interval, securityType, range);
    }

    public async Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType interval, DateTime end, int lookBackPeriod)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        // find out the exact start time
        // roughly -x biz days:
        DateTime roughStart;
        if (interval == IntervalType.OneDay)
            roughStart = end.AddBusinessDays(-lookBackPeriod);

        return await _storage.ReadPrices(security.Id, interval, securityType, end, lookBackPeriod);
    }

    public async Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType interval, DateTime start, DateTime end)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        return await _storage.ReadPrices(security.Id, interval, securityType, start, end);
    }

    public async Task<List<ExtendedOrderBook>> GetOrderBookHistory(Security security, int level, DateTime date)
    {
        var name = DatabaseNames.GetOrderBookTableName(security.Code, security.ExchangeType, level);
        return await _storage.IsTableExists(name, DatabaseNames.MarketData) ? await _storage.ReadOrderBooks(security, level, date) : [];
    }

    public async Task<Dictionary<long, List<DateTime>>> GetSecurityIdToPriceTimes(Security security, IntervalType interval)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dt = await _storage.Query($"SELECT SecurityId, StartTime FROM {tableName} WHERE SecurityId = {security.Id}",
            DatabaseNames.MarketData,
            TypeCode.Int32, TypeCode.DateTime);

        var results = new Dictionary<long, List<DateTime>>();
        foreach (DataRow row in dt.Rows)
        {
            var id = (long)row["SecurityId"];
            var dateTimes = results.GetOrCreate(security.Id);
            dateTimes.Add((DateTime)row["StartTime"]);
        }
        return results;
    }

    public decimal SetSecurityMinQuantity(string code, decimal price)
    {
        var security = GetSecurity(code);
        if (security == null || price == 0) return 0;
        security.MinQuantity = security.MinNotional / price;
        return security.MinQuantity;
    }

    private async Task<List<Security>> ReadStorageAndRefreshCache()
    {
        List<Security> securities = [];
        foreach (var secType in Consts.SupportedSecurityTypes)
        {
            securities.AddRange(await _storage.ReadSecurities(secType, _context.Exchange));
        }
        RefreshCache(securities);
        return securities;
    }

    /// <summary>
    /// Replace old entries and update the reference objects.
    /// </summary>
    /// <param name="securities"></param>
    private void RefreshCache(List<Security> securities)
    {
        lock (_securities)
        {
            Dictionary<string, Security> fxSecurities = new(securities.Count);
            for (int i = 0; i < securities.Count; i++)
            {
                var s = securities[i];
                var secType = SecurityTypeConverter.Parse(s.Type);
                _securities[s.Id] = s;

                // only the first appearance of a code will be cached!
                if (!_securitiesByCode.ContainsKey(s.Code))
                {
                    _securitiesByCode[s.Code] = s;
                }

                _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange))] = s.Id;

                // mark the fx for next loop; it includes both fx and assets
                if (secType == SecurityType.Fx)
                    fxSecurities[s.Code] = s;
            }
            foreach (var security in _securities.Values)
            {
                if (!security.Currency.IsBlank())
                {
                    security.QuoteSecurity = fxSecurities!.GetOrDefault(security.Currency)
                        ?? throw Exceptions.InvalidSecurity(security.Currency, "Cannot find currency asset code from existed fx entries.");
                }
                if (security.FxInfo != null)
                {
                    if (!security.FxInfo.BaseCurrency.IsBlank() && fxSecurities.TryGetValue(security.FxInfo.BaseCurrency, out var baseSecurity))
                    {
                        security.FxInfo.BaseSecurity = baseSecurity;
                    }
                    if (!security.FxInfo.QuoteCurrency.IsBlank() && fxSecurities.TryGetValue(security.FxInfo.QuoteCurrency, out var quoteSecurity))
                    {
                        security.FxInfo.QuoteSecurity = quoteSecurity;
                        security.QuoteSecurity = quoteSecurity;
                        security.Currency = security.FxInfo.QuoteCurrency;
                    }
                    if (security.FxInfo.IsAsset)
                    {
                        // asset's quote asset is itself
                        // no base asset
                        security.FxInfo.BaseSecurity = null;
                        security.FxInfo.QuoteSecurity = security;
                        // avoid circular reference which breaks serialization
                        //var simplified = new Security { Id = security.Id, Code = security.Code, SecurityType = SecurityType.Fx };
                        //security.QuoteSecurity = simplified;
                        security.QuoteSecurity = security;
                        security.Currency = security.Code;
                    }
                }
            }
        }
    }

    private void RefreshCache(Security s)
    {
        lock (_securities)
        {
            _securities[s.Id] = s;
            _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange))] = s.Id;
        }
    }
}
