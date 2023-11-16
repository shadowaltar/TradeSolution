using Syncfusion.UI.Xaml.Charts;
using System.Windows.Controls;

namespace TradeDesk.Views;
/// <summary>
/// Interaction logic for OverviewView.xaml
/// </summary>
public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
    }

    private void DateTimeAxis_LabelCreated(object sender, LabelCreatedEventArgs e)
    {
        DateTimeAxisLabel dateTimeLabel = (DateTimeAxisLabel)e.AxisLabel;
        bool isTransition = dateTimeLabel.IsTransition;

        switch (dateTimeLabel.IntervalType)
        {
            case DateTimeIntervalType.Days:
                {
                    if (isTransition)
                        e.AxisLabel.LabelContent = dateTimeLabel.Position.FromOADate().ToString("MMM-dd");
                    else
                        e.AxisLabel.LabelContent = dateTimeLabel.Position.FromOADate().ToString("dd");
                }
                break;

            case DateTimeIntervalType.Hours:
                {
                    if (isTransition)
                        e.AxisLabel.LabelContent =
                        dateTimeLabel.Position.FromOADate().ToString("MMM-dd");

                    else
                        e.AxisLabel.LabelContent =
                        dateTimeLabel.Position.FromOADate().ToString("dd");
                }
                break;
        }
    }
}
