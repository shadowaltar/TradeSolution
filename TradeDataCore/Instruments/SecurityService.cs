using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.Database;

namespace TradeDataCore.Instruments;
public class SecurityService : ISecurityService
{
    public async Task<List<Security>> GetSecurities(ExchangeType exchange, SecurityType securityType)
    {
        var exchStr = ExchangeTypeConverter.ToString(exchange);
        return await Storage.ReadSecurities(exchStr, securityType);
    }
}
