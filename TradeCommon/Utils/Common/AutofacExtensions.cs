using Autofac;
using Autofac.Builder;

namespace Common;
public static class AutofacExtensions
{
    public static IRegistrationBuilder<TImpl, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingleton<T, TImpl>(this ContainerBuilder builder, object? key = null)
        where TImpl : notnull, T
        where T : notnull
    {
        return key != null
            ? builder.RegisterType<TImpl>().Keyed<T>(key).SingleInstance()
            : builder.RegisterType<TImpl>().As<T>().SingleInstance();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingleton<T>(this ContainerBuilder builder)
        where T : notnull
    {
        return builder.RegisterType<T>().AsSelf().SingleInstance();
    }

    public static IRegistrationBuilder<object, SimpleActivatorData, SingleRegistrationStyle> RegisterSingletonInstance<T>(this ContainerBuilder builder, object instance)
        where T : notnull
    {
        return builder.RegisterInstance(instance).As<T>().SingleInstance();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingleton<T>(this ContainerBuilder builder, object key)
        where T : notnull
    {
        return builder.RegisterType<T>().Keyed<T>(key).SingleInstance();
    }
}
