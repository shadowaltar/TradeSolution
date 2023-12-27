using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDesk.Services;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;
    private string _url = "";
    private string _title = "";
    private string _selectedSecurityCode = "";
    private string _orderRelatedUpdateTime = "";
    private string _tradeRelatedUpdateTime = "";

    public string ServerUrl { get => _url; set => SetValue(ref _url, value); }
    public ObservableCollection<string> SecurityCodes { get; } = [];
    public ClientSession? Session { get; private set; }
    public ICommand Connect { get; }

    public OverviewViewModel OverviewViewModel { get; }
    public OrderViewModel OrderViewModel { get; }
    public OrderStateViewModel OrderStateViewModel { get; }
    public TradeViewModel TradeViewModel { get; }
    public AssetViewModel AssetViewModel { get; }
    public AssetStateViewModel AssetStateViewModel { get; }
    public PositionViewModel PositionViewModel { get; }

    public string Title { get => _title; set => SetValue(ref _title, value); }

    public string SelectedSecurityCode
    {
        get => _selectedSecurityCode; set
        {
            SetValue(ref _selectedSecurityCode, value);
            SecurityCodeChanged?.Invoke(value);
        }
    }

    public string OrderRelatedUpdateTime { get => _orderRelatedUpdateTime; set => SetValue(ref _orderRelatedUpdateTime, value); }

    public string TradeRelatedUpdateTime { get => _tradeRelatedUpdateTime; set => SetValue(ref _tradeRelatedUpdateTime, value); }

    public event Action<string>? SecurityCodeChanged;

    public MainViewModel(Server server,
                         OverviewViewModel overviewViewModel,
                         OrderViewModel orderViewModel,
                         OrderStateViewModel orderStateViewModel,
                         TradeViewModel tradeViewModel,
                         AssetViewModel assetViewModel,
                         AssetStateViewModel assetStateViewModel,
                         PositionViewModel positionViewModel)
    {
        _server = server;
        OverviewViewModel = overviewViewModel;
        OrderViewModel = orderViewModel;
        OrderStateViewModel = orderStateViewModel;
        TradeViewModel = tradeViewModel;
        AssetViewModel = assetViewModel;
        AssetStateViewModel = assetStateViewModel;
        PositionViewModel = positionViewModel;
        Connect = new DelegateCommand(PerformConnect);

        SecurityCodes.AddRange("BTCUSDT", "BTCFDUSD");
        SelectedSecurityCode = SecurityCodes.First();
    }

    public void Initialize(string serverUrlWithPort, ClientSession session)
    {
        ServerUrl = serverUrlWithPort ?? throw Exceptions.Impossible();
        Session = session ?? throw Exceptions.Impossible();

        _server.Initialize(ServerUrl, session.SessionToken);
        Title = $"Trading Desk [{Session.Environment}] [{Session.Exchange}] [{ServerUrl}]";
    }

    public void Reset()
    {
        SecurityCodeChanged = null;
    }

    private void PerformConnect()
    {
        OrderViewModel.Refreshed -= OnOrdersRefreshed;
        OrderViewModel.Refreshed += OnOrdersRefreshed;
        OrderViewModel.Initialize(this);
        TradeViewModel.Refreshed -= OnTradesRefreshed;
        TradeViewModel.Refreshed += OnTradesRefreshed;
        TradeViewModel.Initialize(this);

        OverviewViewModel.Initialize(this);
        OrderStateViewModel.Initialize(this);

        SecurityCodeChanged?.Invoke(SelectedSecurityCode);
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
