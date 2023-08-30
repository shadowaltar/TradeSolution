using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;

namespace TradeDataCore.Instruments;
public interface ISecurityService
{
    Task<List<Security>> GetSecurities(SecurityType securityType, ExchangeType exchange = ExchangeType.Unknown, bool requestExternal = false);
    Task<List<Security>> GetSecurities(List<int> securityIds, bool requestExternal = false);
    Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType, bool requestExternal = false);
    Task<Security?> GetSecurity(int securityId, bool requestExternal = false);
}
