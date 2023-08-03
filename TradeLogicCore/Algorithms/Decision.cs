using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Algorithms;
public class Decision
{
    public int SecurityId { get; set; }

    public OrderType OrderType { get; set; } = OrderType.Limit;

    /// <summary>
    /// Defines if the order has stop(-loss) / take-profit / trailing-stop features.
    /// <see cref="OrderType.Market"/> and <see cref="OrderType.Limit"/> are not valid here.
    /// </summary>
    public OrderType SubOrderType { get; set; } = OrderType.Stop;

    public decimal ProposedLimitPrice { get; set; } = 0;

    public decimal ProposedQuantity { get; set; } = 0;

    public DateTime ProposedTradingTime { get; set; }

    public decimal ActualLimitPrice { get; set; } = 0;

    public decimal ActualQuantity { get; set; } = 0;

    public decimal ActualTradingTime { get; set; } = 0;

    public double StopLossRatio { get; set; } = 0.05;

    public decimal StopLossQuantity { get; set; } = 0;

    public Side Side { get; set; } = Side.None;

    public bool IsClosingPosition { get; set; } = false;

    public bool ExecuteAt { get; set; }
}
