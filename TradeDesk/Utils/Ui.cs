using Autofac;
using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TradeCommon.Runtime;
using TradeDesk.ViewModels;
using IContainer = Autofac.IContainer;

namespace TradeDesk.Utils;

public static class Ui
{
    private static readonly Dictionary<Type, Type> _viewToViewModels = [];

    public static Window MainWindow => Application.Current.MainWindow;

    public static void Invoke(Action action)
    {
        Application.Current.Dispatcher.Invoke(() => action());
    }

    public static void BeginInvoke(Action action)
    {
        Application.Current.Dispatcher.BeginInvoke(() => action());
    }

    public static void RegisterViewAndViewModels(ContainerBuilder builder)
    {
        var viewTypes = ReflectionUtils.GetTypes($"{typeof(App).Namespace}.{nameof(Views)}")
            .Where(t => t.IsSubclassOf(typeof(UserControl)) || t.IsSubclassOf(typeof(Window))).ToList();
        var viewModelTypes = ReflectionUtils.GetTypes($"{typeof(App).Namespace}.{nameof(ViewModels)}")
            .Where(t => t.IsAssignableTo(typeof(INotifyPropertyChanged)) && !t.IsAbstract).ToList();

        // pair up from view to view model types
        foreach (var viewType in viewTypes)
        {
            var targetViewModelTypeName = viewType.Name + "Model";
            var targetViewModelType = viewModelTypes.FirstOrDefault(t => t.Name == targetViewModelTypeName);
            if (targetViewModelType != null)
            {
                _viewToViewModels[viewType] = targetViewModelType;
            }
        }

        foreach (var type in viewTypes.Union(viewModelTypes))
        {
            builder.RegisterSingleton(type);
        }
    }

    /// <summary>
    /// Resolve a view and a view model, and set it as view's DataContext.
    /// </summary>
    /// <typeparam name="TV"></typeparam>
    /// <typeparam name="TVM"></typeparam>
    /// <param name="container"></param>
    /// <returns></returns>
    public static (TV, TVM) ResolveAndSetDataContext<TV, TVM>(this IContainer container) where TV : ContentControl where TVM : INotifyPropertyChanged
    {
        var vt = typeof(TV);
        var vmt = typeof(TVM);
        if (_viewToViewModels.TryGetValue(vt, out var viewModelType) && viewModelType != vmt)
            throw Exceptions.Invalid("View and view model types mismatch: " + vt.Name + " vs " + vmt.Name);
        var v = container.Resolve<TV>();
        var vm = container.Resolve<TVM>();
        v.DataContext = vm;
        if (vmt is IHasView<TV> hasView)
        {
            hasView.View = v;
        }
        return (v, vm);
    }
}
