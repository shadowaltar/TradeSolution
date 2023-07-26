namespace TradeCommon.Essentials.Portfolios;
public class ProfitLoss
{
    public int SecurityId { get; set; }
    public decimal Value { get; set; }
    public decimal StartPrice { get; set; }
    public decimal EndPrice { get; set; }
    public DateTime RealizeTime { get; set; }
    public decimal Quantity { get; set; }
}
