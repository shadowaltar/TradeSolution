using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;

namespace Common;
public static class AutofacExtensions
{
    public static void RegisterSingleton<T, TImpl>(this ContainerBuilder builder, object? key = null)
        where TImpl : notnull, T
        where T : notnull
    {
        if (key != null)
            builder.RegisterType<TImpl>().Keyed<T>(key).SingleInstance();
        else
            builder.RegisterType<TImpl>().As<T>().SingleInstance();
    }

    public static void RegisterSingleton<T>(this ContainerBuilder builder)
        where T : notnull
    {
        builder.RegisterType<T>().AsSelf().SingleInstance();
    }

    public static void RegisterSingleton<T>(this ContainerBuilder builder, object key)
        where T : notnull
    {
        builder.RegisterType<T>().Keyed<T>(key).SingleInstance();
    }
}
