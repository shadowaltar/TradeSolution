using System;
using System.Windows.Input;
using TradeCommon.Essentials;
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

    private void OnSecurityCodeChanged(string securityCode)
    {
        View.StopLive();
    }

    private void OnNextOhlc(OhlcPrice price)
    {
        View.UpdateOhlc(price, SelectedIntervalTimeSpan);
    }

    private void PerformStartLive()
    {
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
            _server.SubscribeOhlc();
        }
    }

    public TimeSpan SelectedIntervalTimeSpan { get; private set; }
}
