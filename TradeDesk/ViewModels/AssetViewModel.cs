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
public class AssetViewModel : AbstractViewModel
{
    private PeriodicTimer _timer;
    private readonly Server _server;

    private Asset selectedAsset;

    public Asset SelectedAsset { get => selectedAsset; set => SetValue(ref selectedAsset, value); }

    public ObservableCollection<Asset> Assets { get; } = new();

    public event Action<List<Asset>, DateTime> Refreshed;

    public string? SecurityCode { get; set; }

    public AssetViewModel(Server server)
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

        List<Asset> assets = await _server.GetAssets();
        Process(assets);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await _timer.WaitForNextTickAsync())
        {
            if (SecurityCode.IsBlank()) return;

            assets = await _server.GetAssets();
            assets = assets.Where(a => a.Quantity != 0 || a.LockedQuantity != 0).ToList();
            Process(assets);
        }

        void Process(List<Asset> assets)
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
