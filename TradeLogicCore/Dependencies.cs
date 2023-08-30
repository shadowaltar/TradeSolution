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

    public static bool IsRegistered { get; private set; } = false;

    public static void Register(BrokerType broker, ExchangeType exchange, EnvironmentType environment, ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();

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

        // setup context
        var context = container.Resolve<Context>();
        context.Setup(environment, exchange, broker, ExternalNames.GetBrokerId(broker));
        IsRegistered = true;
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

            builder.RegisterSingleton<Context>();
            builder.RegisterSingleton<Core>();
            builder.RegisterSingleton<IServices, Services.Services>();
        }
    }
}
