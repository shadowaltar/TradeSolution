using Autofac;
using Common;
using TradeCommon.Externals;
using TradeConnectivity.Binance.Services;

namespace TradeConnectivity.Binance;
public class Dependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<IExternalQuotationManagement, Quotation>();
        builder.RegisterSingleton<IExternalReferenceManagement, Reference>();
        builder.RegisterSingleton<IExternalConnectivityManagement, Connectivity>();
    }
}
