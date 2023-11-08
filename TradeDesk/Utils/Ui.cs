using System;
using System.Windows;

namespace TradeDesk.Utils;

public static class Ui
{
    public static void Invoke(Action action)
    {
        Application.Current.Dispatcher.Invoke(() => action());
    }

    public static Window MainWindow => Application.Current.MainWindow;
}
