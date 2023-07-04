using DevExpress.Data;
using DevExpress.XtraGrid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using TradeApp.Essentials;
using TradeApp.Services;
using TradeApp.Views.Widgets;
using GridView = DevExpress.XtraGrid.Views.Grid.GridView;

namespace TradeApp.ViewModels.Widgets;

public sealed class DepthViewModel : AbstractViewModel, IDisposable, IViewAwared
{
    private Color _bidColor;
    private Color _askColor;

    private DepthGridController? _gridController;

    public string Ticker { get => Headline.Ticker; set => Headline.Ticker = value; }
    public string SecurityName { get => Headline.SecurityName; set => Headline.SecurityName = value; }
    public DepthHeadlineViewModel Headline { get; set; }
    public ObservableCollection<DepthDisplayItem> Items { get; set; } = new();
    public Color BidColor { get => _bidColor; set => SetValue(ref _bidColor, value); }
    public Color AskColor { get => _askColor; set => SetValue(ref _askColor, value); }

    public int DepthCount { get; set; } = 5;

    public DepthViewModel()
    {
        Headline = new DepthHeadlineViewModel();
        Reset();
    }

    public void Reset()
    {
        Items.Clear();
        var c = DepthCount * 2;
        for (int i = 0; i < c; i++)
        {
            Items.Add(new DepthDisplayItem());
        }

        BidColor = Colors.DarkGreen;
        AskColor = Colors.DarkRed;
        Headline.BidColor = BidColor;
        Headline.AskColor = AskColor;
        Headline.IsHeadlineBidAskVisible = true;
        Headline.BestBid = 0;
        Headline.BestAsk = 0;
    }

    public void Connect(IDepthDataService depthDataService)
    {
        depthDataService.DepthUpdated += OnDepthUpdated;
    }

    private void OnDepthUpdated(string ticker, DepthItem item)
    {
        if (ticker != Ticker) return;
        if (item.Depth >= DepthCount || item.Depth < 0) return; // invalid depth or excessive depth

        var index = item.IsBid ? DepthCount + item.Depth : DepthCount - item.Depth - 1;
        Items[index].Price = item.Price;
    }

    public void SetView(Control view)
    {
        _gridController = new DepthGridController(((DepthView)view).DepthGrid, Items);
        _gridController.IsGroupBoxVisible = false;
    }

    public void Dispose()
    {
        _gridController?.Dispose();
    }
}

public class DepthHeadlineViewModel : AbstractViewModel
{
    private string _ticker = string.Empty;
    private string _securityName = string.Empty;
    private bool _isHeadlineMidVisible;
    private bool _isHeadlineBidAskVisible;
    private double _mid;
    private double _bestBid;
    private double _bestAsk;
    private Color _bidColor;
    private Color _askColor;

    public string Ticker { get => _ticker; set => SetValue(ref _ticker, value); }
    public string SecurityName { get => _securityName; set => SetValue(ref _securityName, value); }
    public bool IsHeadlineMidVisible { get => _isHeadlineMidVisible; set => SetValue(ref _isHeadlineMidVisible, value); }
    public bool IsHeadlineBidAskVisible { get => _isHeadlineBidAskVisible; set => SetValue(ref _isHeadlineBidAskVisible, value); }
    public double Mid { get => _mid; set => SetValue(ref _mid, value); }
    public double BestBid { get => _bestBid; set => SetValue(ref _bestBid, value); }
    public double BestAsk { get => _bestAsk; set => SetValue(ref _bestAsk, value); }
    public Color BidColor { get => _bidColor; set => SetValue(ref _bidColor, value); }
    public Color AskColor { get => _askColor; set => SetValue(ref _askColor, value); }
}

public class DepthGridController : IDisposable
{
    private GridControl? _grid;
    private RealTimeSource? _realTimeSource;
    private bool isGroupBoxVisible;
    private GridView _view;

    public DepthGridController(GridControl grid, ObservableCollection<DepthDisplayItem> items)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _realTimeSource = new RealTimeSource { DataSource = items };
        _grid.DataSource = _realTimeSource;
        _view = (GridView)_grid.DefaultView;
    }

    public bool IsGroupBoxVisible
    {
        get => isGroupBoxVisible;
        internal set
        {
            isGroupBoxVisible = value;
            _view.OptionsView.ShowGroupPanel = value;
        }
    }

    public void Dispose()
    {
        if (_grid != null)
        {
            _grid.DataSource = null;
            _grid.Dispose();
            _grid = null;
        }
        _realTimeSource?.Dispose();
        _realTimeSource = null;
    }
}

public class DepthDisplayItem : AbstractViewModel
{
    private double? _price;

    public double? Price { get => _price; set => SetValue(ref _price, value); }

    public BidAsk BidAsk { get; set; }

    public override string? ToString()
    {
        return "Price: " + _price;
    }
}