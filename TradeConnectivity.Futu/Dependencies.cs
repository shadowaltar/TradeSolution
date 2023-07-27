using Autofac;
using Common;
using TradeCommon.Externals;
using TradeConnectivity.Futu.Services;

namespace TradeConnectivity.Futu;
public class Dependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSingleton<IExternalQuotationManagement, Quotation>();
        builder.RegisterSingleton<IExternalExecutionManagement, Execution>();
    }
}
