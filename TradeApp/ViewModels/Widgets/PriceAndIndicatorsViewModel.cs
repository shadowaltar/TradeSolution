using DevExpress.Xpf.Docking;
using DevExpress.XtraCharts;
using System;
using System.Windows.Controls;
using TradeApp.Views.Widgets;

namespace TradeApp.ViewModels.Widgets;

public class PriceAndIndicatorsViewModel : AbstractViewModel, IDisposable, IViewAwared
{
    public ChartControl PriceChartControl { get; internal set; }

    public void Dispose()
    {
        PriceChartControl.Dispose();
    }

    public void SetPrices()
    {

    }

    public void SetIndicators()
    {

    }

    public void SetView(Control view)
    {
        if (view is PriceAndIndicatorsView actualView)
        {
            var rootHost = actualView.RootHost;
            rootHost.Child = PriceChartControl = new ChartControl();
        }
        else if (view is LayoutPanel panel && panel.Content is Control innerControl)
        {
            SetView(innerControl);
        }
    }
}
