using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalExecutionManagement
{
    bool Initialize(User user);

    void PlaceOrder(Order order);

    void CancelOrder(Order order);

    void ModifyOrder(Order order);

    void CancelAllOrder(Order order);

    event OrderPlacedCallback OrderPlaced;
    event OrderModifiedCallback OrderModified;
    event OrderCanceledCallback OrderCanceled;
    event AllOrderCanceledCallback AllOrderCanceled;
}
