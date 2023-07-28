using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Essentials.Trading;

/// <summary>
/// The activity which represents maker and taker forms a deal by matching a price and quantity.
/// It can also be called as a Deal.
/// One order object may result in zero or more trades immediately or in a period of time.
/// </summary>
public class Trade
{
    public const long DefaultId = 0;

    /// <summary>
    /// Unique trade id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Security id.
    /// </summary>
    public int SecurityId { get; set; }

    /// <summary>
    /// The order id associated with this trade.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// The trade id associated with this trade provided by the broker.
    /// </summary>
    public long ExternalTradeId { get; set; } = DefaultId;

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// </summary>
    public long ExternalOrderId { get; set; } = DefaultId;

    /// <summary>
    /// Trade execution time.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Side of this trade.
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// The execution price of this trade.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The executed quantity in this trade.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The fee incurred in this trade.
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// The trade object is coarse such that we don't have
    /// info to determine who owns it or which order is this trade being related to.
    /// Usually it is a trade observed in the market which is
    /// not related to current user.
    /// </summary>
    public bool IsCoarse { get; set; } = false;
    public override string ToString()
    {
        return $"[{Id}][{ExternalTradeId}][{Time:yyMMdd-HHmmss}] secId:{SecurityId}, p:{Price}, q:{Quantity}, side:{Side}";
    }
}
