﻿using Autofac;
using Autofac.Builder;

namespace Common;
public static class AutofacExtensions
{
    public static IRegistrationBuilder<TImpl, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterSingleton<T, TImpl>(this ContainerBuilder builder, object? key = null)
        where TImpl : notnull, T
        where T : notnull
    {
        if (key != null)
            return builder.RegisterType<TImpl>().Keyed<T>(key).SingleInstance();
        else
            return builder.RegisterType<TImpl>().As<T>().SingleInstance();
    }

    public static void RegisterSingleton<T>(this ContainerBuilder builder)
        where T : notnull
    {
        builder.RegisterType<T>().AsSelf().SingleInstance();
    }

    public static void RegisterSingletonInstance<T>(this ContainerBuilder builder, object instance)
        where T : notnull
    {
        builder.RegisterInstance(instance).AsSelf().SingleInstance();
    }

    public static void RegisterSingleton<T>(this ContainerBuilder builder, object key)
        where T : notnull
    {
        builder.RegisterType<T>().Keyed<T>(key).SingleInstance();
    }
}
