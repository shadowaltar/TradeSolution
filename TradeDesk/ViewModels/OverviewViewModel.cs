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
public class OverviewViewModel : AbstractViewModel, IHasView<OverviewView>
{
    private readonly Server _server;

    private bool _isLive;

    private IntervalType _selectedInterval;
    private Security? _security;

    public TimeSpan SelectedIntervalTimeSpan { get; private set; }
    public OverviewView? View { get; set; }
    public ICommand StartLive { get; }

    public IntervalType SelectedInterval { get => _selectedInterval; set => SetValue(ref _selectedInterval, value, v => SelectedIntervalTimeSpan = v.ToTimeSpan()); }

    public bool IsLive { get => _isLive; set => SetValue(ref _isLive, value); }


    public OverviewViewModel(Server server)
    {
        _server = server;
        _server.OhlcReceived += OnNextOhlc;
        StartLive = new DelegateCommand(PerformStartLive);
    }

    public void Initialize(MainViewModel mainViewModel)
    {
        mainViewModel.SecurityCodeChanged += OnSecurityCodeChanged;
    }

    private async void OnSecurityCodeChanged(string securityCode)
    {
        if (View == null) return;
        View.StopLive();
        var securities = await _server.GetSecurities();
        _security = securities?.FirstOrDefault(s => s.Code == securityCode);
    }

    private void OnNextOhlc(OhlcPrice price)
    {
        if (View == null) return;
        View.UpdateOhlc(price, SelectedIntervalTimeSpan);
    }

    private void PerformStartLive()
    {
        if (View == null) return;
        if (_security == null) return;

        if (IsLive)
        {
            _server.UnsubscribeOhlc();
            View.StopLive();
            IsLive = false;
        }
        else
        {
            IsLive = true;
            View.StartLive(SelectedIntervalTimeSpan);
            _server.SubscribeOhlc(_security, SelectedInterval);
        }
    }
}
