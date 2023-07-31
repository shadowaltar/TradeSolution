using Autofac;
using Common;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Constants;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public static class Dependencies
{
    [NotNull]
    public static IComponentContext? ComponentContext { get; private set; }

    public static void Register(string externalName, ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();
        builder.RegisterModule<DependencyModule>();
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
        switch (externalName)
        {
            case ExternalNames.Binance:
                builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                break;
            case ExternalNames.Futu:
                builder.RegisterModule<TradeConnectivity.Futu.Dependencies>();
                break;
            case ExternalNames.CryptoSimulator:
                builder.RegisterModule<TradeConnectivity.CryptoSimulator.Dependencies>();
                break;
        }

        var container = builder.Build();

        ComponentContext = container.Resolve<IComponentContext>();
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

            // position sizing
            builder.RegisterSingleton<EvenPositionSizingLogic>();
            // algorithms
            builder.RegisterSingleton<IAlgorithm, Rumi>("Rumi");
        }
    }
}
