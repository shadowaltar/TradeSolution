using log4net.Config;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using TradeCommon.Constants;
using Common;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;
using System.Collections.Generic;
using TradeCommon.Essentials.Trading;
using System;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;
    private string _url;
    private readonly DelegateCommand connect;

    public string ServerUrl { get => _url; set => SetValue(ref _url, value); }
    public ObservableCollection<string> SecurityCodes { get; } = new();
    public ClientSession? Session { get; private set; }
    public ICommand Connect { get; }


    public OverviewViewModel OverviewViewModel { get; }
    public OrderViewModel OrderViewModel { get; }
    public OrderStateViewModel OrderStateViewModel { get; }
    public TradeViewModel TradeViewModel { get; }
    public MainView Window { get; private set; }

    private string title;

    public string Title { get => title; set => SetValue(ref title, value); }

    private string selectedSecurityCode;

    public string SelectedSecurityCode
    {
        get => selectedSecurityCode; set
        {
            SetValue(ref selectedSecurityCode, value);
            OrderViewModel.SecurityCode = SelectedSecurityCode;
        }
    }

    private string orderRelatedUpdateTime;

    public string OrderRelatedUpdateTime { get => orderRelatedUpdateTime; set => SetValue(ref orderRelatedUpdateTime, value); }

    public MainViewModel()
    {
        _server = new Server();
        SecurityCodes.AddRange("BTCUSDT", "BTCFDUSD");

        OverviewViewModel = new OverviewViewModel(_server);
        OrderViewModel = new OrderViewModel(_server);
        OrderStateViewModel = new OrderStateViewModel(_server);
        TradeViewModel = new TradeViewModel(_server);

        Connect = new DelegateCommand(PerformConnect);
    }

    public void Initialize(MainView mainView)
    {
        Window = mainView;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();

        Window.Hide();

        var lv = new LoginView();
        var lvm = new LoginViewModel();
        lv.DataContext = lvm;
        lvm.AfterLogin += OnLoggedIn;
        lv.ShowDialog();

        void OnLoggedIn(string token)
        {
            lvm.AfterLogin -= OnLoggedIn;
            lv.Close();

            _server.Setup(lvm.ServerUrl, token);

            ServerUrl = lvm.ServerUrl;
            Session = new(lvm.UserName,
                          lvm.Account,
                          lvm.EnvironmentType,
                          lvm.ExchangeType,
                          ExternalNames.Convert(lvm.ExchangeType),
                          token);
            Title = $"Trading Desk [{Session.Environment}] [{Session.Exchange}] [{ServerUrl}]";
            Window.Show();
        }
    }

    private void PerformConnect()
    {
        OrderViewModel.Refreshed -= OnOrdersRefreshed;
        OrderViewModel.Refreshed += OnOrdersRefreshed;
        OrderViewModel.Initialize();

        OverviewViewModel.Initialize();

        OrderStateViewModel.Initialize();

        TradeViewModel.Refreshed -= OnTradesRefreshed;
        TradeViewModel.Refreshed += OnTradesRefreshed;
        TradeViewModel.Initialize();
    }

    private void OnTradesRefreshed(List<Trade> list, DateTime time)
    {
        throw new NotImplementedException();
    }

    private void OnOrdersRefreshed(List<Order> list, DateTime time)
    {
        OrderRelatedUpdateTime = time.ToString("yyMMdd-HHmmss");
    }

    internal void Initialize(bool obj)
    {

    }
}
