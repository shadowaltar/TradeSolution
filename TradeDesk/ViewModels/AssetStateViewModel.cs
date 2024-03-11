using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using TradeCommon.Essentials.Portfolios;
using TradeDesk.Services;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class AssetStateViewModel : AbstractViewModel
{
    private PeriodicTimer _timer;
    private readonly Server _server;
    private AssetState selectedAssetState;
    public AssetState SelectedAssetState { get => selectedAssetState; set => SetValue(ref selectedAssetState, value); }
    public ObservableCollection<AssetState> AssetStates { get; } = [];

    public event Action<List<AssetState>, DateTime> Refreshed;

    public string? SecurityCode { get; set; }

    public AssetStateViewModel(Server server)
    {
        _server = server;
    }

    public void Initialize()
    {
        PeriodicQuery();
    }

    public async void PeriodicQuery()
    {
        if (SecurityCode.IsBlank()) return;
        _timer?.Dispose();

        List<AssetState> states = await _server.GetAssetStates();
        Process(states);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            states = await _server.GetAssetStates();
            states = states.Where(a => a.Quantity != 0).ToList();
            Process(states);
        }

        void Process(List<AssetState> states)
        {
            Ui.Invoke(() =>
            {
                (List<AssetState> existingOnly, List<AssetState> newOnly) = AssetStates.FindDifferences(states);
                foreach (AssetState o in existingOnly)
                {
                    AssetStates.Remove(o);
                }
                foreach (AssetState o in newOnly)
                {
                    AssetStates.Add(o);
                }
            });
            Refreshed?.Invoke(states, DateTime.UtcNow);
        }
    }
}
