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
    private int _candleCount = 100;

    public int CandleCount
    {
        get => _candleCount; set
        {
            _candleCount = value;
        }
    }
    public OverviewView()
    {
        InitializeComponent();

        OhlcData = new OHLC[CandleCount];
    }

    public void StartLive(TimeSpan interval)
    {
        var lastTime = DateTime.Now;

        _timer?.Dispose();
        _timer = new Timer(TimerRender, null, 0, 100);
    }

    private void TimerRender(object? state)
    {
        mainPlot?.Refresh();
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
        // init logic
        if (OhlcData[^1] == null)
        {
            var lastTime = price.T;
            for (int i = OhlcData.Length - 1; i >= 0; i--)
            {
                lastTime -= timeSpan;
                OhlcData[i] = new OHLC(0, 0, 0, 0, lastTime, timeSpan);
            }

            _candlePlot ??= mainPlot.Plot.AddCandlesticks(OhlcData);
        }

        var candle = Convert(price, timeSpan);

        Array.Copy(OhlcData, 1, OhlcData, 0, OhlcData.Length - 1);
        OhlcData[^1] = candle;
    }

    public OHLC Convert(OhlcPrice price, TimeSpan timeSpan)
    {
        return new OHLC((double)price.O, (double)price.H, (double)price.L, (double)price.C, price.T, timeSpan);
    }
}
