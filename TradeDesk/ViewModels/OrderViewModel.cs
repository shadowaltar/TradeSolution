using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;

public class OrderViewModel : AbstractViewModel
{
    protected PeriodicTimer _timer;
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

    public async void PeriodicQuery()
    {
        if (SecurityCode.IsBlank()) return;

        _timer?.Dispose();

        List<Order> orders = await _server.GetOrders(SecurityCode, DateTime.UtcNow.AddDays(-1));
        Process(orders);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            orders = await _server.GetOrders(SecurityCode);
            Process(orders);
        }

        void Process(List<Order> orders)
        {
            Ui.Invoke(() =>
            {
                (List<Order> existingOnly, List<Order> newOnly) = Orders.FindDifferences(orders);
                foreach (Order o in existingOnly)
                {
                    Orders.Remove(o);
                }
                foreach (Order o in newOnly)
                {
                    Orders.Add(o);
                }
            });
            Refreshed?.Invoke(orders, DateTime.UtcNow);
        }
    }

    private async void CancelAll()
    {
        foreach (Order order in await _server.GetOpenOrders())
        {
            Order result = await _server.CancelOrder(order);
        }
    }

    private async void Cancel()
    {
        if (SelectedOrder == null) return;
        Order result = await _server.CancelOrder(SelectedOrder);
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

    private void Select()
    {
    }
}
