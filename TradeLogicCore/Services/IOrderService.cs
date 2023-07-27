using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface IOrderService
{
   event Action<Order> NewOrder;
}