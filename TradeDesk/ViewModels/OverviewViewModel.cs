using System;
using System.Linq;
using System.Windows.Input;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;
public class OverviewViewModel : AbstractViewModel
{
    private readonly Server _server;

    private DelegateCommand _startLive;
    private bool _isLive;

    private IntervalType _selectedInterval;
    private Security _security;

    public IntervalType SelectedInterval { get => _selectedInterval; set => SetValue(ref _selectedInterval, value, v => SelectedIntervalTimeSpan = v.ToTimeSpan()); }

    public ICommand StartLive => _startLive ??= new DelegateCommand(PerformStartLive);

    public bool IsLive { get => _isLive; set => SetValue(ref _isLive, value); }

    public OverviewView View { get; internal set; }

    public OverviewViewModel(MainViewModel mainViewModel, Server server)
    {
        _server = server;
        _server.OhlcReceived += OnNextOhlc;

        mainViewModel.SecurityCodeChanged += OnSecurityCodeChanged;
    }

    public void Initialize()
    {

    }

    private async void OnSecurityCodeChanged(string securityCode)
    {
        if (View == null) return;
        View.StopLive();
        var securities = await _server.GetSecurities();
        _security = securities.FirstOrDefault(s => s.Code == securityCode);
    }

    private void OnNextOhlc(OhlcPrice price)
    {
        if (View == null) return;
        View.UpdateOhlc(price, SelectedIntervalTimeSpan);
    }

    private void PerformStartLive()
    {
        if (View == null) return;
        if (IsLive)
        {
            _server.UnsubscribeOhlc();
            View.StopLive();
            IsLive = false;
        }
        else
        {
            IsLive = true;
            View.StartLive();
            _server.SubscribeOhlc(_security, SelectedInterval);
        }
    }

    public TimeSpan SelectedIntervalTimeSpan { get; private set; }
}
