using System;
using System.Windows.Input;
using TradeDesk.Services;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;
    private string _url;
    private readonly DelegateCommand connect;

    public string Url { get => _url; set => SetValue(ref _url, value); }


    public ICommand Connect { get; }


    public OverviewViewModel OverviewViewModel { get; private set; }
    public OrderViewModel OpenOrderViewModel { get; private set; }
    public OrderViewModel ErrorOrderViewModel { get; private set; }
    public OrderViewModel CancelledOrderViewModel { get; private set; }
    public OrderStateViewModel OrderStateViewModel { get; private set; }
    public MainView Window { get; internal set; }

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

        OverviewViewModel = new OverviewViewModel(_server);

        Connect = new DelegateCommand(PerformConnect);
    }

    private void PerformConnect()
    {
        OpenOrderViewModel.Initialize();
    }

    internal void Initialize(bool obj)
    {

    }
}
