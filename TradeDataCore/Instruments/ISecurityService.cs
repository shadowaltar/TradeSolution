using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;

namespace TradeDataCore.Instruments;
public interface ISecurityService
{
    Task<List<Security>?> GetSecurities(ExchangeType exchange, SecurityType securityType);
    Task<List<Security>?> GetSecurities(ExchangeType exchange, List<int> securityIds);
    Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType);
}
