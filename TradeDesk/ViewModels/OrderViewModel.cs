using System.Collections.ObjectModel;
using System.Windows.Input;
using TradeCommon.Essentials.Trading;
using TradeDesk.Utils;
using TradeDesk.Views;
using TradeLogicCore.Services;

namespace TradeDesk.ViewModels;

public class OrderViewModel : AbstractViewModel
{
    private readonly IOrderService _orderService;
    private bool _isOrderToolBarVisible;

    public bool IsOrderToolBarVisible { get => _isOrderToolBarVisible; set => SetValue(ref _isOrderToolBarVisible, value); }

    public ObservableCollection<Order> Orders { get; } = new();

    public Order? SelectedOrder { get; private set; }

    public ICommand SelectedCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CancelAllCommand { get; }

    public OrderViewModel(IOrderService orderService)
    {
        _orderService = orderService;
        SelectedCommand = new DelegateCommand(Select);
        CreateCommand = new DelegateCommand(CreateOrder);
        CancelCommand = new DelegateCommand(Cancel);
        CancelAllCommand = new DelegateCommand(CancelAll);
    }

    private async void CancelAll()
    {
        foreach (var order in _orderService.GetOpenOrders())
        {
            var result = await _orderService.CancelOrder(order);
        }
    }

    private async void Cancel()
    {
        if (SelectedOrder == null) return;
        var result = await _orderService.CancelOrder(SelectedOrder);
    }

    public void CreateOrder()
    {
        var view = new NewOrderView();
        var vm = new NewOrderViewModel();
        vm.Parent = this;
        view.DataContext = vm;
        view.ShowDialog();
    }

    private void Select()
    {
    }
}
