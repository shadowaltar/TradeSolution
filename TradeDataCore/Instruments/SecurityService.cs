using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Database;

namespace TradeDataCore.Instruments;
public class SecurityService : ISecurityService
{
    public async Task<List<Security>> GetSecurities(ExchangeType exchange, SecurityType securityType)
    {
        var exchStr = ExchangeTypeConverter.ToString(exchange);
        return await Storage.ReadSecurities(exchStr, securityType);
    }

    public async Task<Security> GetSecurity(string code, ExchangeType exchange, SecurityType securityType)
    {
        var exchStr = ExchangeTypeConverter.ToString(exchange);
        return await Storage.ReadSecurity(exchStr, code, securityType);
    }
}
