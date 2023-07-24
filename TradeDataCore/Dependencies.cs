using Autofac;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeDataCore.Quotation;
using TradeDataCore.StaticData;

namespace TradeDataCore;
public class Dependencies
{
    public static IContainer? Container { get; private set; }

    public static void Register(ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();
        builder.RegisterModule<DependencyModule>();
        Container = builder.Build();
    }

    public class DependencyModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<FutuQuotationEngine>().Named<IQuotationEngine>(ExternalNames.Futu).SingleInstance();

            builder.RegisterType<DataServices>().As<IDataServices>().SingleInstance();
            builder.RegisterType<HistoricalMarketDataService>().As<IHistoricalMarketDataService>().SingleInstance();
            builder.RegisterType<RealTimeMarketDataService>().As<IRealTimeMarketDataService>().SingleInstance();
            builder.RegisterType<FinancialStatsDataService>().As<IFinancialStatsDataService>().SingleInstance();

            builder.RegisterType<SecurityService>().As<ISecurityService>();
        }
    }
}
