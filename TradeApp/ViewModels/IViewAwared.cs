using System.Windows.Controls;
using TradeApp.UX;

namespace TradeApp.ViewModels;

/// <summary>
/// For a ViewModel. <see cref="WorkspaceManager"/> will help to inject the view once this interface is detected.
/// </summary>
public interface IViewAwared
{
    void SetView(Control view);
}
