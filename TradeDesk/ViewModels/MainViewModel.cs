using System;
using System.Threading;
using TradeDesk.Services;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;

    public OrderViewModel OpenOrderViewModel { get; private set; }
    public OrderViewModel ErrorOrderViewModel { get; private set; }
    public OrderViewModel CancelledOrderViewModel { get; private set; }
    public OrderStateViewModel OrderStateViewModel { get; private set; }

    public MainViewModel()
    {
        _server = new Server();
        OpenOrderViewModel = new OrderViewModel(_server)
        {
            IsOrderToolBarVisible = true
        };
        ErrorOrderViewModel = new OrderViewModel(_server)
        {
            IsOrderToolBarVisible = false
        };
        CancelledOrderViewModel = new OrderViewModel(_server)
        {
            IsOrderToolBarVisible = false
        };

        OrderStateViewModel = new OrderStateViewModel(_server);
    }

    public async void PeriodicQuery()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync())
        {
        }
    }
}
