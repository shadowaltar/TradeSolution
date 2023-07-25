using Autofac;
using TradeCommon.Constants;
using TradeLogicCore.Execution;
using TradeLogicCore.Instruments;

namespace TradeLogicCore;
public class Dependencies
{
    public static IContainer? Container { get; private set; }

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
            // Put your common registrations here.
            builder.RegisterType<FutuEngine>().Named<IExecutionEngine>(ExternalNames.Futu).SingleInstance();
            builder.RegisterType<BinanceEngine>().Named<IExecutionEngine>(ExternalNames.Binance).SingleInstance();

            builder.RegisterType<StockScreener>().As<IStockScreener>().SingleInstance();
        }
    }
}
