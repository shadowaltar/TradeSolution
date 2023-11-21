using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;

namespace TradeDesk.ViewModels;
public class TradeViewModel : AbstractViewModel
{
    private readonly Server _server;
    private PeriodicTimer _timer;

    public event Action<List<Trade>, DateTime> Refreshed;

    public ObservableCollection<Trade> Trades { get; } = new();

    public string? SecurityCode { get; set; }

    public TradeViewModel(Server server)
    {
        _server = server;
    }

    public async void PeriodicQuery()
    {
        if (SecurityCode.IsBlank()) return;
        _timer?.Dispose();

        var trades = await _server.GetTrades(SecurityCode, true, DateTime.UtcNow.AddDays(-1));
        Process(trades);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            trades = await _server.GetTrades(SecurityCode, false, DateTime.MinValue);
            Process(trades);
        }

        void Process(List<Trade> trades)
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
            Refreshed?.Invoke(trades, DateTime.UtcNow);
        }
    }

    internal void Initialize()
    {
        PeriodicQuery();
    }
}
