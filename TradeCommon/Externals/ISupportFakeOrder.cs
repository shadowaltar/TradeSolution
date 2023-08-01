using TradeCommon.Essentials.Trading;

namespace TradeCommon.Externals;

public interface ISupportFakeOrder
{
    Task SendFakeOrder(Order order);
}