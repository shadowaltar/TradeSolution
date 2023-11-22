using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeDesk.Services;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class AssetStateViewModel : AbstractViewModel
{
    private PeriodicTimer _timer;
    private readonly Server _server;
    public ObservableCollection<AssetState> AssetStates { get; } = new();

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
            states = states.Where(a => a.Quantity != 0 || a.LockedQuantity != 0).ToList();
            Process(states);
        }

        void Process(List<AssetState> states)
        {
            Ui.Invoke(() =>
            {
                (List<Asset> existingOnly, List<Asset> newOnly) = Assets.FindDifferences(assets);
                foreach (Asset o in existingOnly)
                {
                    Assets.Remove(o);
                }
                foreach (Asset o in newOnly)
                {
                    Assets.Add(o);
                }
            });
            Refreshed?.Invoke(assets, DateTime.UtcNow);
        }
    }
}
