using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeDataCore.Quotation;
public class BinanceQuotationEngine : IQuotationEngine
{
    public Task<ExternalConnectionState> InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> SubscribeAsync(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalConnectionState> UnsubscribeAsync(Security security)
    {
        throw new NotImplementedException();
    }
}
