using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Quotes;
using TradeDesk.ViewModels;

namespace TradeDesk.Views;
/// <summary>
/// Interaction logic for OverviewView.xaml
/// </summary>
public partial class OverviewView : UserControl
{
    public OHLC[] OhlcData { get; }

    private FinancePlot _candlePlot;
    private Timer _timer;

    public OverviewView()
    {
        InitializeComponent();

        OhlcData = new OHLC[100];
    }

    public void StartLive()
    {
        _candlePlot ??= mainPlot.Plot.AddCandlesticks(OhlcData);
        _timer?.Dispose();
        _timer = new Timer(TimerRender, null, 0, 100);
    }

    private void TimerRender(object? state)
    {
        mainPlot.Refresh();
    }

    public void StopLive()
    {
        _timer?.Dispose();
    }

    private void ViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var vm = (OverviewViewModel)DataContext;
        vm.View = this;
    }

    public void UpdateOhlc(OhlcPrice price, TimeSpan timeSpan)
    {
        var candle = new OHLC((double)price.O, (double)price.H, (double)price.L, (double)price.C, price.T, timeSpan);
        Array.Copy(OhlcData, 1, OhlcData, 0, OhlcData.Length - 1);
        OhlcData[^1] = candle;
    }
}
