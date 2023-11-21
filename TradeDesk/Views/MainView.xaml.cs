using System.Windows;

namespace TradeDesk.ViewModels;
/// <summary>
/// Interaction logic for MainView.xaml
/// </summary>
public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.Initialize(this);
    }
}
