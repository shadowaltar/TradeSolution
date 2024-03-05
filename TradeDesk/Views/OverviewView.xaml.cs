using ScottPlot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using TradeCommon.Essentials.Quotes;
using TradeDesk.Utils;

namespace TradeDesk.Views;
/// <summary>
/// Interaction logic for OverviewView.xaml
/// </summary>
public partial class OverviewView : UserControl
{
    public List<OHLC> OhlcData { get; } = [];

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

        OhlcData = new List<OHLC>(CandleCount);
    }

    public void StartLive(TimeSpan interval)
    {
        var lastTime = DateTime.Now;

        _timer?.Dispose();
        _timer = new Timer(TimerRender, null, 0, 100);
    }

    private void TimerRender(object? state)
    {
        Ui.Invoke(() => mainPlot?.Refresh());
    }

    public void StopLive()
    {
        _timer?.Dispose();
    }

    public void UpdateOhlc(OhlcPrice price, TimeSpan timeSpan)
    {
        // init logic
        if (OhlcData.Count < CandleCount)
        {
            var lastTime = price.T;
            for (int i = OhlcData.Count - 1; i >= 0; i--)
            {
                lastTime -= timeSpan;
                OhlcData[i] = new OHLC(0, 0, 0, 0, lastTime, timeSpan);
            }

            mainPlot.Plot.Add.OHLC(OhlcData);
        }

        var candle = Convert(price, timeSpan);

        OhlcData[^1] = candle;
    }

    public OHLC Convert(OhlcPrice price, TimeSpan timeSpan)
    {
        return new OHLC((double)price.O, (double)price.H, (double)price.L, (double)price.C, price.T, timeSpan);
    }
}
