using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Timers;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;

public class OrderViewModel : AbstractViewModel
{
    private bool _isOrderToolBarVisible;
    private PeriodicTimer _timer;
    private readonly Server _server;

    public ObservableCollection<Order> Orders { get; } = new();
    public Order? SelectedOrder { get; private set; }
    public ICommand SelectedCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CancelAllCommand { get; }

    public event Action<List<Order>, DateTime> Refreshed;

    public string? SecurityCode { get; set; }

    public OrderViewModel(Server server)
    {
        SelectedCommand = new DelegateCommand(Select);
        CreateCommand = new DelegateCommand(CreateOrder);
        CancelCommand = new DelegateCommand(Cancel);
        CancelAllCommand = new DelegateCommand(CancelAll);
        _server = server;
    }

    public void Initialize()
    {
        PeriodicQuery();
    }

    private async void CancelAll()
    {
        foreach (var order in await _server.GetOpenOrders())
        {
            var result = await _server.CancelOrder(order);
        }
    }

    private async void Cancel()
    {
        if (SelectedOrder == null) return;
        var result = await _server.CancelOrder(SelectedOrder);
    }

    public void CreateOrder()
    {
        var view = new NewOrderView();
        var vm = new NewOrderViewModel
        {
            Parent = this
        };
        view.DataContext = vm;
        view.ShowDialog();
    }

    public async void PeriodicQuery()
    {
        if (SecurityCode.IsBlank()) return;

        _timer?.Dispose();

        var orders = await _server.GetOrders(SecurityCode, true, DateTime.UtcNow.AddDays(-1));
        Process(orders);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            orders = await _server.GetOrders(SecurityCode, false, DateTime.MinValue);
            Process(orders);
        }

        void Process(List<Order> orders)
        {
            var (existingOnly, newOnly) = Orders.FindDifferences(orders);
            foreach (var o in existingOnly)
            {
                Orders.Remove(o);
            }
            foreach (var o in newOnly)
            {
                Orders.Add(o);
            }
            Refreshed?.Invoke(orders, DateTime.UtcNow);
        }
    }

    private void Select()
    {
    }
}
