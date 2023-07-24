using Autofac;
using static TradeDataCore.Dependencies;

namespace TradePort;

public class Dependencies
{
    public static IContainer? Container { get; private set; }

    public static void Register(ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();

        // external dependencies
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
        builder.RegisterModule<TradeLogicCore.Dependencies.DependencyModule>();

        Container = builder.Build();
    }
}
