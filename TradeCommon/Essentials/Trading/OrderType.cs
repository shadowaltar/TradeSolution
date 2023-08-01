using System.ComponentModel;

namespace TradeCommon.Essentials.Trading;

public enum OrderType
{
    /// <summary>
    /// Execute the order using whatever current market price is.
    /// </summary>
    [Description("MARKET")]
    Market,
    /// <summary>
    /// Execute the order by a price specified.
    /// </summary>
    [Description("LIMIT")]
    Limit,
    /// <summary>
    /// The order becomes a market order whenever a specific price is touched at least once.
    /// For a buy order it expects the price to rise to a certain value, vice versa.
    /// </summary>
    [Description("STOP_LOSS")]
    Stop,
    /// <summary>
    /// The order becomes a limit order whenever a specific price is touched at least once.
    /// For a buy order it expects the price to rice to a certain value, vice versa.
    /// </summary>
    [Description("STOP_LOSS_LIMIT")]
    StopLimit,
    [Description("TAKE_PROFIT")]
    TakeProfit,
    [Description("TAKE_PROFIT_LIMIT")]
    TakeProfitLimit,
    /// <summary>
    /// The order becomes a market order whenever a specific price is touched at least once.
    /// For a buy order it expects the price to fall to a certain value, vice versa.
    /// </summary>
    MarketIfTouched,
    /// <summary>
    /// The order becomes a limit order whenever a specific price is touched at least once.
    /// For a buy order it expects the price to fall to a certain value, vice versa.
    /// </summary>
    LimitIfTouched,
    /// <summary>
    /// A stop order with a price near current market price. When the market price moves in
    /// one's favor, the order price will move along with the direction by certain amount or
    /// ratio.
    /// For a buy order initially the trailing-stop price is lower than market price, vice versa.
    /// </summary>
    TrailingStop,
    /// <summary>
    /// A stop limit order with a price near current market price. When the market price moves in
    /// one's favor, the order's limit price will move along with the direction by certain amount or
    /// ratio.
    /// For a buy order initially the trailing-stop price is lower than market price, vice versa.
    /// </summary>
    TrailingStopLimit,
    /// <summary>
    /// Binance specific order type.
    /// </summary>
    [Description("LIMIT_MAKER")]
    LimitMaker
}