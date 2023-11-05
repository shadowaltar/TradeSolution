using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;

public class OrderViewModel
{
    public ObservableCollection<Order> Orders { get; } = new();

    public ICommand SelectedCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CancelAllCommand { get; }

    public OrderViewModel()
    {
        SelectedCommand = new DelegateCommand(Select);
        CreateCommand = new DelegateCommand(Create);
        CancelCommand = new DelegateCommand(Create);
    }

    private void Select()
    {
    }
}
