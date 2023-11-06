using System;
using System.Threading;
using TradeLogicCore.Services;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    public OrderViewModel OpenOrderViewModel { get; private set; }
    public OrderViewModel ErrorOrderViewModel { get; private set; }
    public OrderViewModel CancelledOrderViewModel { get; private set; }
    public OrderStateViewModel OrderStateViewModel { get; private set; }

    public MainViewModel(IOrderService orderService)
    {
        OpenOrderViewModel = new OrderViewModel(orderService);
        OpenOrderViewModel.IsOrderToolBarVisible = true;
        ErrorOrderViewModel = new OrderViewModel(orderService);
        ErrorOrderViewModel.IsOrderToolBarVisible = false;
        CancelledOrderViewModel = new OrderViewModel(orderService);
        CancelledOrderViewModel.IsOrderToolBarVisible = false;

        OrderStateViewModel = new OrderStateViewModel(orderService);
    }

    public async void PeriodicQuery()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync())
        {
        }
    }
}
