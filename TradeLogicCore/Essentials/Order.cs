namespace TradeLogicCore.Essentials;
public class Order
{
    public int Id { get; set; }
    public int SecurityId { get; set; }
    public OrderType Type { get; set; } = OrderType.Market;
    public OrderSide BuySell { get; set; }
    public decimal Price { get; set; }
    public decimal LimitPrice { get; set; } = 0;
    public decimal StopPrice { get; set; } = 0;
    public decimal Quantity { get; set; }
    public DateTime DateTime { get; set; }
    public OrderFilling OrderFilling { get; set; } = OrderFilling.FillAllPricesAndKillRest;
    public OrderValidity Validity { get; set; } = OrderValidity.Day;
    public int AccountId { get; set; }
    public int StrategyId { get; set; }
}
