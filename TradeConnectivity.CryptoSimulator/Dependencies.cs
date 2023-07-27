using Autofac;
using Common;
using TradeCommon.Externals;
using TradeConnectivity.CryptoSimulator.Services;

namespace TradeConnectivity.CryptoSimulator;
public class Dependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<IExternalQuotationManagement, Quotation>();
        builder.RegisterSingleton<IExternalReferenceManagement, Reference>();
        builder.RegisterSingleton<IExternalExecutionManagement, Execution>();
        builder.RegisterSingleton<IExternalHistoricalMarketDataManagement, HistoricalMarketData>();
        builder.RegisterSingleton<IExternalConnectivityManagement, Connectivity>();
    }
}
