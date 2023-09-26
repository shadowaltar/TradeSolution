using Common;
using Iced.Intel;
using Microsoft.Identity.Client.Extensions.Msal;
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
    private readonly Dictionary<(string code, ExchangeType exchange, SecurityType securityType), int> _mapping = new();
    private readonly ApplicationContext _context;
    private readonly IStorage _storage;
    private bool _isInitialized;

    public SecurityService(ApplicationContext context, IStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<List<Security>> Initialize()
    {
        if (!_context.IsInitialized) throw Exceptions.ContextNotInitialized();
        if (_isInitialized) return _securities.Values.ToList();

        var secTypes = Enum.GetValues<SecurityType>();
        var exchangeTypes = Enum.GetValues<ExchangeType>();
        List<Security> securities = new();
        foreach (var secType in secTypes)
        {
            foreach (var exchangeType in exchangeTypes)
            {
                securities.AddRange(await _storage.ReadSecurities(secType, exchangeType));
            }
        }
        RefreshCache(securities);

        _isInitialized = true;
        return securities;
    }

    public async Task<List<Security>> GetAllSecurities(bool requestDatabase = false)
    {
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

    public async Task<Security?> GetSecurity(string code, string exchange, string securityType, bool requestExternal = false)
    {
        var exchangeType = ExchangeTypeConverter.Parse(exchange);
        var secType = SecurityTypeConverter.Parse(securityType);
        return await GetSecurity(code, exchangeType, secType, requestExternal);
    }

    public async Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType, bool requestExternal = false)
    {
        if (requestExternal)
        {
            var security = await _storage.ReadSecurity(exchange, code, securityType);
            if (security != null)
            {
                await GetAllSecurities();
            }
            return security;
        }
        else
        {
            lock (_securities)
            {
                var id = _mapping.TryGetValue((code, exchange, securityType), out var temp) ? temp : 0;
                return id <= 0 ? null : _securities.GetValueOrDefault(id);
            }
        }
    }

    public async Task<Security?> GetSecurity(int securityId, bool requestExternal = false)
    {
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
        if (code.IsBlank()) return null;
        return _securitiesByCode.ThreadSafeGet(code);
    }

    public Security GetSecurity(int securityId)
    {
        return _securities.ThreadSafeGet(securityId) ?? throw Exceptions.InvalidSecurityId(securityId);
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

                _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange), secType)] = s.Id;

                // mark the fx for next loop; it includes both fx and assets
                if (secType == SecurityType.Fx)
                    fxSecurities[s.Code] = s;
            }
            foreach (var security in _securities.Values)
            {
                if (!security.Currency.IsBlank())
                {
                    security.CurrencyAsset = fxSecurities!.GetOrDefault(security.Currency)
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
                        security.CurrencyAsset = quoteAsset;
                        security.Currency = security.FxInfo.QuoteCurrency;
                    }
                    if (security.FxInfo.IsAsset)
                    {
                        // asset's quote asset is itself
                        // no base asset
                        security.FxInfo.BaseAsset = null;
                        security.FxInfo.QuoteAsset = security;
                        security.CurrencyAsset = security;
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
            _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange), SecurityTypeConverter.Parse(s.Type))] = s.Id;
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
}
