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
        builder.RegisterType<FutuEngine>().Named<IExecutionEngine>(ExternalNames.Futu);
        builder.RegisterType<BinanceEngine>().Named<IExecutionEngine>(ExternalNames.Binance);

        builder.RegisterType<StockScreener>().Named<ISecurityScreener>(SecurityTypes.Stock);

        // register depending project's DI entries
        TradeDataCore.Dependencies.Register(builder);

        Container = builder.Build();
    }

    public static T Resolve<T>(string name) where T : notnull
    {
        return Container!.ResolveNamed<T>(name);
    }
}
