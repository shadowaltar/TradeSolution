using TradeLogicCore.Services;

namespace TradeDesk.ViewModels;
public class OrderStateViewModel : AbstractViewModel
{
    private readonly IOrderService _orderService;

    public OrderStateViewModel(IOrderService orderService)
    {
        _orderService = orderService;
    }
}
