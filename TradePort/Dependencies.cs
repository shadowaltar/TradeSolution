using Autofac;

namespace TradePort;

public static class Dependencies
{
    public static IComponentContext? Container { get; private set; }

    public static void Register(ContainerBuilder? builder = null)
    {
        builder ??= new ContainerBuilder();

        // external dependencies
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
        builder.RegisterModule<TradeLogicCore.Dependencies.DependencyModule>();

        Container = builder.Build();
    }
}
