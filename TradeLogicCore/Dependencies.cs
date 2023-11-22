using Autofac;
using Common;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeLogicCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public static class Dependencies
{
    [NotNull]
    public static IComponentContext? ComponentContext { get; private set; }

    public static Context UpdateBrokerModule(IComponentContext existingContainer, BrokerType broker)
    {
        var scope = (Autofac.Core.Lifetime.LifetimeScope)existingContainer;
        scope.BeginLifetimeScope(builder =>
        {
            switch (broker)
            {
                case BrokerType.Binance:
                    builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                    break;
                    //case BrokerType.Futu:
                    //    _builder.RegisterModule<TradeConnectivity.Futu.Dependencies>();
                    //    break;
                    //case BrokerType.Simulator:
                    //    _builder.RegisterModule<TradeConnectivity.CryptoSimulator.Dependencies>();
                    //    break;
            }
        }); // should not dispose this!
        ComponentContext = existingContainer;
        return existingContainer.Resolve<Context>();
    }

    public static Context Register(BrokerType broker)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<DependencyModule>();
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
        switch (broker)
        {
            case BrokerType.Binance:
                builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                break;
                //case BrokerType.Futu:
                //    _builder.RegisterModule<TradeConnectivity.Futu.Dependencies>();
                //    break;
                //case BrokerType.Simulator:
                //    _builder.RegisterModule<TradeConnectivity.CryptoSimulator.Dependencies>();
                //    break;
        }

        var container = builder.Build();
        ComponentContext = container.Resolve<IComponentContext>();
        return container.Resolve<Context>();
    }

    public class DependencyModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterSingleton<IStockScreener, StockScreener>();

            builder.RegisterSingleton<IOrderService, OrderService>();
            builder.RegisterSingleton<ITradeService, TradeService>();
            builder.RegisterSingleton<IPortfolioService, PortfolioService>();
            builder.RegisterSingleton<IAlgorithmService, AlgorithmService>();
            builder.RegisterSingleton<IAdminService, AdminService>();

            builder.RegisterType<Context>().As<Context>().As<ApplicationContext>().SingleInstance();
            //var context = new Context();
            //builder.RegisterSingletonInstance<Context>(context);
            //builder.RegisterSingletonInstance<ApplicationContext>(context);
            builder.RegisterSingleton<Core>();
            builder.RegisterSingleton<DataPublisher>();
            builder.RegisterSingleton<IServices, Services.Services>();
        }
    }
}
