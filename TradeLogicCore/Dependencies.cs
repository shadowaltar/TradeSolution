using Autofac;
using TradeCommon.Constants;
using TradeCommon.Utils.Common;
using TradeLogicCore.Execution;
using TradeLogicCore.Instruments;
using TradeLogicCore.PortfolioManagement;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public static class Dependencies
{
    public static IComponentContext? Container { get; private set; }

    public static void Register(ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();

        builder.RegisterModule<DependencyModule>();
        // external dependencies
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();

        Container = builder.Build();
    }

    public class DependencyModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterSingleton<IExecutionEngine, FutuEngine>(ExternalNames.Futu);
            builder.RegisterSingleton<IExecutionEngine, BinanceEngine>(ExternalNames.Binance);

            builder.RegisterSingleton<IStockScreener, StockScreener>();

            builder.RegisterSingleton<IOrderService, OrderService>();
            builder.RegisterSingleton<ITradeService, TradeService>();
            builder.RegisterSingleton<IPortfolioEngine, PortfolioEngine>();
        }
    }
}
