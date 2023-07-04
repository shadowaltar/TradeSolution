using System.Collections.ObjectModel;
using TradeApp.Models;

namespace TradeApp.ViewModels.Widgets;

public class SimplePriceViewModel : AbstractViewModel
{
    private double _lastClosePrice;

    public ObservableCollection<ClosePrice> Prices { get; } = new();

    public double LastClosePrice { get => _lastClosePrice; set => SetValue(ref _lastClosePrice, value); }
}
