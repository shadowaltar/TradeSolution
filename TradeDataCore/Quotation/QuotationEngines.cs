using Autofac;
using Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeDataCore.Quotation;
public class QuotationEngines
{
    private static readonly ILog _log = Logger.New();

    private readonly IContainer _container;

    public IQuotationEngine? FutuQuotationEngine { get; private set; }
    public IQuotationEngine? BinanceQuotationEngine { get; private set; }

    public QuotationEngines(IContainer container)
    {
        _container = container;
    }

    public async Task Initialize(string externalName)
    {
        ExternalConnectionState state;

        switch (externalName)
        {
            case ExternalNames.Futu:
                FutuQuotationEngine = _container.ResolveKeyed<IQuotationEngine>(ExternalNames.Futu);
                state = await FutuQuotationEngine.InitializeAsync();
                break;
            case ExternalNames.Binance:
                BinanceQuotationEngine = _container.ResolveKeyed<IQuotationEngine>(ExternalNames.Binance);
                state = await BinanceQuotationEngine.InitializeAsync();
                break;
            default:
                throw new NotImplementedException();
        }
        _log.Info($"Initialized {externalName} quotation engine with state: {state}");
    }
}
