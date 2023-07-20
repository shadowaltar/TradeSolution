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
    public string? ExternalTradeId { get; set; }

    /// <summary>
    /// The order id associated with this trade provided by the broker.
    /// </summary>
    public string? ExternalOrderId { get; set; }

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
    /// Indicates if the trade is on maker or trader side.
    /// </summary>
    public MakerTaker MakerTaker { get; set; } = MakerTaker.Unknown;
}
