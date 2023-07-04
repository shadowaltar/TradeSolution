using DevExpress.Xpf.Charts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TradeApp.Models;

namespace TradeApp.ViewModels.Widgets;

public class CandlePriceViewModel : AbstractViewModel
{
    private ChartIntervalItem _selectedInterval;

    public ObservableCollection<CandlePrice> Prices { get; } = new();
    public ChartIntervalItem SelectedInterval { get => _selectedInterval; set => SetValue(ref _selectedInterval, value); }
    public List<ChartIntervalItem> IntervalsSource { get; private set; }
}
