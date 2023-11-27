using System;
using System.Windows;
using System.Windows.Controls;
using TradeDesk.ViewModels;

namespace TradeDesk.Views;
/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : Window
{
    private bool _removeThisViewOnly;

    public LoginView()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        if (!_removeThisViewOnly)
            Application.Current.Shutdown();
    }

    public new void Close()
    {
        _removeThisViewOnly = true;
    }

    public void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext != null)
        {
            ((dynamic)DataContext).UserPassword = ((PasswordBox)sender).Password;
        }
    }

    public void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext != null)
        {
            ((dynamic)DataContext).AdminPassword = ((PasswordBox)sender).Password;
        }
    }
}
