namespace TradeCommon.Essentials.Portfolios;

public interface ILongShortEntry
{
    /// <summary>
    /// The quantity, which is usually short - long quantity.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The weighted average price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The notional amount which is usually quantity * price.
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// All the long trades' weighted average price.
    /// </summary>
    public decimal LongPrice { get; set; }

    /// <summary>
    /// All the long trades' notional amount.
    /// </summary>
    public decimal LongNotional { get; set; }
    /// <summary>
    /// All the long trades' sum of quantity.
    /// </summary>
    public decimal LongQuantity { get; set; }

    /// <summary>
    /// All the short trades' sum of quantity.
    /// </summary>
    public decimal ShortQuantity { get; set; }

    /// <summary>
    /// All the short trades' weighted average price.
    /// </summary>
    public decimal ShortPrice { get; set; }

    /// <summary>
    /// All the short trades' notional amount.
    /// </summary>
    public decimal ShortNotional { get; set; }
}