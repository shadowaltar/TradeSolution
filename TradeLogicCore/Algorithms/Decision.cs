using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Algorithms;
public class Decision
{

    public Side Side { get; set; }

    public OrderType OrderType { get; set; } = OrderType.Limit;

    public decimal LimitPrice { get; set; }

    public decimal Quantity { get; set; }


    public bool ShallExecute { get; set; }
}
