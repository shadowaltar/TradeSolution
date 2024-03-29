﻿using System.Windows;
using System.Windows.Controls;

namespace TradeDesk.Views;

/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : Window
{
    public LoginView()
    {
        InitializeComponent();
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
