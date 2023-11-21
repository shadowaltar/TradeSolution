using System;
using System.Collections.ObjectModel;
using TradeCommon.Essentials.Misc;
using TradeCommon.Essentials.Quotes;
using TradeDesk.Services;

namespace TradeDesk.ViewModels;
public class OverviewViewModel : AbstractViewModel
{
    private readonly Server _server;

    private string priceFormat;

    public OverviewViewModel(Server server)
    {
        _server = server;
    }

    public ObservableCollection<OhlcPrice> OhlcPrices { get; } = new();
    public ObservableCollection<TimeAndValue> Volumes { get; } = new();

    public string PriceFormat { get => priceFormat; set => SetValue(ref priceFormat, value); }

    private DateTime? chartMinDateTime;

    public DateTime? ChartMinDateTime { get => chartMinDateTime; set => SetValue(ref chartMinDateTime, value); }

    private DateTime? chartMaxDateTime;

    public DateTime? ChartMaxDateTime { get => chartMaxDateTime; set => SetValue(ref chartMaxDateTime, value); }

    internal void Initialize()
    {
        throw new NotImplementedException();
    }
}
