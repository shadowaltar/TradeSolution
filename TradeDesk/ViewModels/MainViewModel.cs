using Common;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;
    private string _url;

    public string ServerUrl { get => _url; set => SetValue(ref _url, value); }
    public ObservableCollection<string> SecurityCodes { get; } = new();
    public ClientSession? Session { get; private set; }
    public ICommand Connect { get; }


    public OverviewViewModel OverviewViewModel { get; }
    public OrderViewModel OrderViewModel { get; }
    public OrderStateViewModel OrderStateViewModel { get; }
    public TradeViewModel TradeViewModel { get; }
    public AssetViewModel AssetViewModel { get; }
    public AssetStateViewModel AssetStateViewModel { get; }
    public PositionViewModel PositionViewModel { get; }
    public MainView Window { get; private set; }

    private string title;

    public string Title { get => title; set => SetValue(ref title, value); }

    private string selectedSecurityCode;

    public string SelectedSecurityCode
    {
        get => selectedSecurityCode; set
        {
            SetValue(ref selectedSecurityCode, value);
            SecurityCodeChanged?.Invoke(value);
        }
    }

    private string orderRelatedUpdateTime;

    public string OrderRelatedUpdateTime { get => orderRelatedUpdateTime; set => SetValue(ref orderRelatedUpdateTime, value); }

    private string tradeRelatedUpdateTime;

    public string TradeRelatedUpdateTime { get => tradeRelatedUpdateTime; set => SetValue(ref tradeRelatedUpdateTime, value); }

    public event Action<string>? SecurityCodeChanged;

    public MainViewModel()
    {
        _server = new Server();

        OverviewViewModel = new OverviewViewModel(this, _server);

        OrderViewModel = new OrderViewModel(this, _server);

        OrderStateViewModel = new OrderStateViewModel(_server);
        TradeViewModel = new TradeViewModel(_server);
        AssetViewModel = new AssetViewModel(_server);
        AssetStateViewModel = new AssetStateViewModel(_server);

        Connect = new DelegateCommand(PerformConnect);

        SecurityCodes.AddRange("BTCUSDT", "BTCFDUSD");
        SelectedSecurityCode = SecurityCodes.First();
    }

    public void Initialize(MainView mainView)
    {
        Window = mainView;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();

        Window.Hide();

        var lv = new LoginView();
        var lvm = new LoginViewModel(_server);
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

            // refresh the data in subviews
            SecurityCodeChanged?.Invoke(SelectedSecurityCode);

            Window.Show();
        }
    }

    public void Reset()
    {
        SecurityCodeChanged = null;
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
        TradeRelatedUpdateTime = time.ToString("yyMMdd-HHmmss");
    }

    private void OnOrdersRefreshed(List<Order> list, DateTime time)
    {
        OrderRelatedUpdateTime = time.ToString("yyMMdd-HHmmss");
    }
}
