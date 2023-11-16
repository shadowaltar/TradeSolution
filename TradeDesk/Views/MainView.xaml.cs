using System;
using System.Windows;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;
/// <summary>
/// Interaction logic for MainView.xaml
/// </summary>
public partial class MainView : Window
{
    private LoginView _loginView;
    private LoginViewModel _loginViewModel;

    public MainView()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.Window = this;
        Hide();

        _loginView = new LoginView();
        _loginViewModel = new LoginViewModel();
        _loginView.DataContext = _loginViewModel;
        _loginViewModel.AfterLogin += Initialize;
        _loginView.ShowDialog();
    }

    private void Initialize(bool obj)
    {
        Show();
        _loginViewModel.AfterLogin -= Initialize;
        _loginView.Close();
    }
}
