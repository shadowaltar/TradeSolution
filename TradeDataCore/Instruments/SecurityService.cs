﻿using Common;
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
public class SecurityService : ISecurityService
{
    private readonly Dictionary<int, Security> _securities = new();
    private readonly Dictionary<string, Security> _securitiesByCode = new();
    private readonly Dictionary<(string code, ExchangeType exchange), int> _mapping = new();
    private readonly IStorage _storage;
    public bool IsInitialized { get; private set; }

    public SecurityService(IStorage storage)
    {
        _storage = storage;
    }

    public async Task<List<Security>> Initialize()
    {
        if (IsInitialized) return _securities.Values.ToList();

        var exchangeTypes = Enum.GetValues<ExchangeType>();
        List<Security> securities = new();
        foreach (var secType in Consts.SupportedSecurityTypes)
        {
            foreach (var exchangeType in exchangeTypes)
            {
                securities.AddRange(await _storage.ReadSecurities(secType, exchangeType));
            }
        }
        RefreshCache(securities);

        IsInitialized = true;
        return securities;
    }

    public async Task<List<Security>> GetAllSecurities(bool requestDatabase = false)
    {
        if (!IsInitialized)
            await Initialize();
        if (requestDatabase)
        {
            return await Initialize();
        }
        else
        {
            lock (_securities)
            {
                return _securities.Values.ToList();
            }
        }
    }

    public async Task<List<Security>> GetSecurities(SecurityType secType,
                                                    ExchangeType exchange,
                                                    bool requestDatabase = false)
    {
        if (!IsInitialized)
            await Initialize();
        var exchStr = exchange != ExchangeType.Unknown ? ExchangeTypeConverter.ToString(exchange) : null;
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
                return _securities.Values
                    .Where(s => s.Exchange == exchStr && SecurityTypeConverter.Matches(s.Type, secType))
                    .ToList();
            }
        }
    }

    public List<Security> GetAssets(ExchangeType exchange = ExchangeType.Unknown)
    {
        if (!IsInitialized)
            AsyncHelper.RunSync(Initialize);
        var exchStr = exchange != ExchangeType.Unknown ? ExchangeTypeConverter.ToString(exchange) : null;
        lock (_securities)
        {
            return _securities.Values
                .Where(s => s.Exchange == exchStr && SecurityTypeConverter.Matches(s.Type, SecurityType.Fx) && s.FxInfo?.IsAsset == true)
                .ToList();
        }
    }

    public async Task<List<Security>> GetSecurities(List<int> securityIds, bool requestExternal = false)
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

    public async Task<Security?> GetSecurity(int securityId, bool requestExternal = false)
    {
        if (!IsInitialized)
            await Initialize();
        if (requestExternal)
        {
            var securities = await _storage.ReadSecurities(new List<int> { securityId });
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
        lock (_securities)
        {
            return _securities.Values.FirstOrDefault(s => s.FxInfo?.BaseCurrency == baseCurrency && s.FxInfo?.QuoteCurrency == quoteCurrency);
        }
    }

    public Security GetSecurity(int securityId)
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

    public async Task<(int securityId, int count)> UpsertPrices(int id, IntervalType interval, SecurityType secType, List<OhlcPrice> prices)
    {
        return await _storage.UpsertPrices(id, interval, secType, prices);
    }

    public async Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16)
    {
        return await _storage.ReadPrices(securityId, interval, securityType, start, end, priceDecimalPoints);
    }

    public async Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range)
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
        return await _storage.IsTableExists(name, DatabaseNames.MarketData) ? await _storage.ReadOrderBooks(security, level, date) : new();
    }

    public async Task<Dictionary<int, List<DateTime>>> GetSecurityIdToPriceTimes(Security security, IntervalType interval)
    {
        var securityType = SecurityTypeConverter.Parse(security.Type);
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        var dt = await _storage.Query($"SELECT SecurityId, StartTime FROM {tableName} WHERE SecurityId = {security.Id}",
            DatabaseNames.MarketData,
            TypeCode.Int32, TypeCode.DateTime);

        var results = new Dictionary<int, List<DateTime>>();
        foreach (DataRow row in dt.Rows)
        {
            var id = (int)row["SecurityId"];
            var dateTimes = results.GetOrCreate(security.Id);
            dateTimes.Add((DateTime)row["StartTime"]);
        }
        return results;
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
                    if (!security.FxInfo.BaseCurrency.IsBlank() && fxSecurities.TryGetValue(security.FxInfo.BaseCurrency, out var baseAsset))
                    {
                        security.FxInfo.BaseAsset = baseAsset;
                    }
                    if (!security.FxInfo.QuoteCurrency.IsBlank() && fxSecurities.TryGetValue(security.FxInfo.QuoteCurrency, out var quoteAsset))
                    {
                        security.FxInfo.QuoteAsset = quoteAsset;
                        security.QuoteSecurity = quoteAsset;
                        security.Currency = security.FxInfo.QuoteCurrency;
                    }
                    if (security.FxInfo.IsAsset)
                    {
                        // asset's quote asset is itself
                        // no base asset
                        security.FxInfo.BaseAsset = null;
                        security.FxInfo.QuoteAsset = security;
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
