using Autofac;
using TradeCommon.Constants;
using TradeCommon.Utils.Common;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeDataCore.Quotation;
using TradeDataCore.StaticData;

namespace TradeDataCore;
public class Dependencies
{
    public static IComponentContext? Container { get; private set; }

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
            builder.RegisterSingleton<IQuotationEngine, FutuQuotationEngine>(ExternalNames.Futu);

            builder.RegisterSingleton<IDataServices, DataServices>();
            builder.RegisterSingleton<IHistoricalMarketDataService, HistoricalMarketDataService>();
            builder.RegisterSingleton<IRealTimeMarketDataService, RealTimeMarketDataService>();
            builder.RegisterSingleton<IFinancialStatsDataService, FinancialStatsDataService>();

            builder.RegisterSingleton<QuotationEngines>();
            builder.RegisterSingleton<IQuotationEngine, FutuQuotationEngine>(ExternalNames.Futu);
            builder.RegisterSingleton<IQuotationEngine, BinanceQuotationEngine>(ExternalNames.Binance);

            builder.RegisterSingleton<ISecurityService, SecurityService>();
        }
    }
}
