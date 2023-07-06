namespace TradeLogicCore.Essentials;
public class Position
{
    public int Id { get; set; }
    public int SecurityId { get; set; }
    public decimal Quantity { get; set; }
    public decimal OpenQuantity { get; set; }
    public decimal Notional { get; set; }
    public int LastOrderId { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
}
