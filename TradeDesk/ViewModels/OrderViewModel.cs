using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;

public class OrderViewModel : AbstractViewModel
{
    private bool _isOrderToolBarVisible;
    private readonly Server _server;

    public bool IsOrderToolBarVisible { get => _isOrderToolBarVisible; set => SetValue(ref _isOrderToolBarVisible, value); }

    public ObservableCollection<Order> Orders { get; } = new();
    public Order? SelectedOrder { get; private set; }
    public ICommand SelectedCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CancelAllCommand { get; }


    public OrderViewModel(Server server)
    {
        SelectedCommand = new DelegateCommand(Select);
        CreateCommand = new DelegateCommand(CreateOrder);
        CancelCommand = new DelegateCommand(Cancel);
        CancelAllCommand = new DelegateCommand(CancelAll);
        _server = server;

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
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync())
        {
            _server.GetOrders();
        }
    }

    private void UpdateData(List<Order> orders)
    {
        throw new NotImplementedException();
    }

    private void Select()
    {
    }
}
