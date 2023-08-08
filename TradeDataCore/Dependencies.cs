﻿using Autofac;
using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
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
            builder.RegisterSingleton<IDataServices, DataServices>();
            builder.RegisterSingleton<IHistoricalMarketDataService, HistoricalMarketDataService>();
            builder.RegisterSingleton<IRealTimeMarketDataService, RealTimeMarketDataService>();
            builder.RegisterSingleton<IFinancialStatsDataService, FinancialStatsDataService>();

            builder.RegisterSingleton<MessageBroker<Order>>(nameof(Order));
            builder.RegisterSingleton<MessageBroker<Trade>>(nameof(Trade));
            builder.RegisterSingleton<MessageBroker<Position>>(nameof(Position));
            builder.RegisterSingleton<MessageBroker<IPersistenceTask>>();

            builder.RegisterSingleton<Environments>();
            builder.RegisterSingleton<Persistence>();

            builder.RegisterSingleton<ISecurityService, SecurityService>();
        }
    }
}
