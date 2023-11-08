using System.Windows;

namespace TradeDesk.Utils;
public class MessageBoxes
{
    public static void Error(Window? owner, string message, string title = "Error")
    {
        Ui.Invoke(() =>
        {
            owner ??= Ui.MainWindow;
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    public static bool Warn(Window? owner, string message, string title = "Warning", bool isOkOnly = false)
    {
        MessageBoxResult mbr = MessageBoxResult.Cancel;
        Ui.Invoke(() =>
        {
            owner ??= Ui.MainWindow;
            mbr = MessageBox.Show(owner, message, title, isOkOnly ? MessageBoxButton.OK : MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        });
        if (mbr == MessageBoxResult.OK) return true;
        return false;
    }

    public static bool WarnAsk(Window? owner, string message, string title = "Warning")
    {
        var result = false;
        Ui.Invoke(() =>
        {
            owner ??= Ui.MainWindow;
            var r = MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
                result = true;
        });
        return result;
    }

    public static void Info(Window? owner, string message, string title = "Info")
    {
        Ui.Invoke(() =>
        {
            owner ??= Ui.MainWindow;
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}
