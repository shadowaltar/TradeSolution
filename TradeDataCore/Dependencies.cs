using Autofac;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeDataCore.Quotation;

namespace TradeDataCore;
public class Dependencies
{
    public static IContainer? Container { get; private set; }

    public static void Register(ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();
        
        builder.RegisterType<FutuQuotationEngine>().Named<IQuotationEngine>(ExternalNames.Futu);
        
        builder.RegisterType<RealTimeMarketDataService>().As<IRealTimeMarketDataService>();
        builder.RegisterType<HistoricalMarketDataService>().As<IHistoricalMarketDataService>();
        
        builder.RegisterType<SecurityService>().As<ISecurityService>();

        Container = builder.Build();
    }

    public static T Resolve<T>(string name) where T : notnull
    {
        return Container!.ResolveNamed<T>(name);
    }
}
