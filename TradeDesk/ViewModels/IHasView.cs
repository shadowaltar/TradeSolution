using System.Windows.Controls;

namespace TradeDesk.ViewModels;
public interface IHasView<TV> where TV : ContentControl
{
    TV? View { get; set; }
}
