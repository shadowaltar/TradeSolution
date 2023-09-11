using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeDataCore.Instruments;
public class SecurityService : ISecurityService
{
    private readonly Dictionary<int, Security> _securities = new();
    private readonly Dictionary<(string code, ExchangeType exchange, SecurityType securityType), int> _mapping = new();

    private bool _requestedExternalOnce = false;

    public async Task<List<Security>> GetAllSecurities(bool requestExternal = false)
    {
        if (!_requestedExternalOnce || requestExternal)
        {
            // TODO fix the sync to further minimize multiple database read
            _requestedExternalOnce = true;

            var securities = await Storage.ReadSecurities();
            RefreshCache(securities);
            return securities;
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
                                                    ExchangeType exchange = ExchangeType.Unknown,
                                                    bool requestExternal = false)
    {
        var exchStr = exchange != ExchangeType.Unknown ? ExchangeTypeConverter.ToString(exchange) : null;
        if (!_requestedExternalOnce || requestExternal)
        {
            var securities = await Storage.ReadSecurities(secType, exchStr);
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
        if (!_requestedExternalOnce)
            AsyncHelper.RunSync(() => GetAllSecurities(true));
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
        if (!_requestedExternalOnce || requestExternal)
        {
            var securities = await Storage.ReadSecurities(securityIds);
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
        if (!_requestedExternalOnce || requestExternal)
        {
            var security = await Storage.ReadSecurity(exchange, code, securityType);
            if (security != null)
                RefreshCache(security);
            return security;
        }
        else
        {
            lock (_securities)
            {
                var id = _mapping.TryGetValue((code, exchange, securityType), out var temp) ? temp : -1;
                return id == -1 ? null : _securities.GetValueOrDefault(id);
            }
        }
    }

    public async Task<Security?> GetSecurity(int securityId, bool requestExternal = false)
    {
        if (!_requestedExternalOnce || requestExternal)
        {
            var securities = await Storage.ReadSecurities(new List<int> { securityId });
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

    public Security GetSecurity(int securityId)
    {
        lock (_securities)
        {
            return _securities.GetValueOrDefault(securityId) ?? throw Exceptions.InvalidSecurityId(securityId);
        }
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
                _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange), secType)] = s.Id;

                // mark the fx for next loop; it includes both fx and assets
                if (secType == SecurityType.Fx)
                    fxSecurities[s.Code] = s;
            }
            foreach (var security in _securities.Values)
            {
                if (!security.Currency.IsBlank())
                {
                    security.CurrencyAsset = fxSecurities!.GetOrDefault(security.Currency);
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
                    }
                    if (security.FxInfo.IsAsset)
                    {
                        // asset's quote asset is itself
                        // no base asset
                        security.FxInfo.BaseAsset = null;
                        security.FxInfo.QuoteAsset = security;
                        security.CurrencyAsset = security;
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
}
