using System.Windows;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;
/// <summary>
/// Interaction logic for MainView.xaml
/// </summary>
public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Hide();

        var loginView = new LoginView();
        loginView.DataContext = new LoginViewModel();
        loginView.ShowDialog();
    }
}
