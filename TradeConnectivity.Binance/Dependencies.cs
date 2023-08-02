﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common;
using Microsoft.Extensions.DependencyInjection;
using TradeCommon.Externals;
using TradeConnectivity.Binance.Services;

namespace TradeConnectivity.Binance;
public class Dependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var services = new ServiceCollection();

        var serviceCollection = services.AddHttpClient();
        serviceCollection.AddHttpClient<Quotation>();
        serviceCollection.AddHttpClient<Execution>();
        builder.Populate(services);

        builder.RegisterSingleton<KeyManager>();
        builder.RegisterSingleton<IExternalQuotationManagement, Quotation>();
        builder.RegisterSingleton<IExternalReferenceManagement, Reference>();
        builder.RegisterSingleton<IExternalExecutionManagement, Execution>();
        builder.RegisterSingleton<IExternalHistoricalMarketDataManagement, HistoricalMarketData>();
        builder.RegisterSingleton<IExternalConnectivityManagement, Connectivity>();
    }
}
