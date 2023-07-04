namespace TradeApp.Essentials;


public class DepthItem
{
    public int Depth { get; set; }
    public double? Price { get; set; }
    public int Volume { get; set; }
    public BidAsk BidAsk { get; set; }
}