using Autofac;
using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeDataCore.StaticData;

namespace TradeDataCore;
public class Dependencies
{
    public static IComponentContext Container { get; private set; }

    public static void Register(string? externalName = null, ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();
        builder.RegisterModule<DependencyModule>();
        if (externalName != null)
        {
            switch (externalName)
            {
                case ExternalNames.Binance:
                    builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                    break;
            }
        }

        Container = builder.Build();
    }

    public class DependencyModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterSingleton<IStorage, Storage>();
            builder.RegisterSingleton<IHistoricalMarketDataService, HistoricalMarketDataService>();
            builder.RegisterSingleton<IMarketDataService, RealTimeMarketDataService>();
            builder.RegisterSingleton<DataPublisher>();
            builder.RegisterSingleton<IFinancialStatsDataService, FinancialStatsDataService>();

            builder.RegisterSingleton<MessageBroker<Order>>(nameof(Order));
            builder.RegisterSingleton<MessageBroker<Trade>>(nameof(Trade));
            builder.RegisterSingleton<MessageBroker<Position>>(nameof(Position));

            builder.RegisterSingleton<Persistence>();

            builder.RegisterSingleton<ISecurityService, SecurityService>();
        }
    }
}
