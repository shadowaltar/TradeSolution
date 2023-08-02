using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;

public interface ISupportFakeOrder
{
    Task<ExternalQueryState<Order>> SendFakeOrder(Order order);
}