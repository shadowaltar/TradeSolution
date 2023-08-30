using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Database;
using Common;

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

    public async Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType, bool requestExternal = false)
    {
        if (!_requestedExternalOnce || requestExternal)
        {
            var exchStr = ExchangeTypeConverter.ToString(exchange);
            var security = await Storage.ReadSecurity(exchStr, code, securityType);
            if (security != null)
                RefreshCache(security);
            return security;
        }
        else
        {
            lock (_securities)
            {
                var id = _mapping.TryGetValue((code, exchange, securityType), out var temp) ? -1 : temp;
                if (id == -1) return null;
                return _securities.GetValueOrDefault(id);
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
            lock (_securities)
            {
                return _securities.GetValueOrDefault(securityId);
            }
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
            for (int i = 0; i < securities.Count; i++)
            {
                var s = securities[i];
                _securities[s.Id] = s;
                _mapping[(s.Code, ExchangeTypeConverter.Parse(s.Exchange), SecurityTypeConverter.Parse(s.Type))] = s.Id;
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
