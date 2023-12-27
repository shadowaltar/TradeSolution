using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class TradeViewModel(Server server) : AbstractViewModel
{
    private readonly Server _server = server;
    private PeriodicTimer? _timer;

    public event Action<List<Trade>, DateTime>? Refreshed;

    public ObservableCollection<Trade> Trades { get; } = [];

    public string? SecurityCode { get; set; }

    public async void PeriodicQuery()
    {
        _timer?.Dispose();
        List<Trade> trades;
        if (!SecurityCode.IsBlank())
        {
            trades = await _server.GetTrades(SecurityCode, DateTime.UtcNow.AddDays(-1));
            Process(trades);
        }

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            trades = await _server.GetTrades(SecurityCode);
            Process(trades);
        }

        void Process(List<Trade> trades)
        {
            Ui.Invoke(() =>
            {
                var (existingOnly, newOnly) = Trades.FindDifferences(trades);
                foreach (var o in existingOnly)
                {
                    Trades.Remove(o);
                }
                foreach (var o in newOnly)
                {
                    Trades.Add(o);
                }
            });
            Refreshed?.Invoke(trades, DateTime.UtcNow);
        }

    }

    public void Initialize(MainViewModel mainViewModel)
    {
        mainViewModel.SecurityCodeChanged += OnSecurityCodeChanged;
        PeriodicQuery();
    }

    private void OnSecurityCodeChanged(string code)
    {
        SecurityCode = code;
    }
}
