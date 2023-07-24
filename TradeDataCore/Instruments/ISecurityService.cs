using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;

namespace TradeDataCore.Instruments;
public interface ISecurityService
{
    Task<List<Security>> GetSecurities(ExchangeType exchange, SecurityType securityType);
}
