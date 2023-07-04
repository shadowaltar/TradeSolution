namespace TradeApp.Essentials;

public record struct DepthLevel(int Depth, double? Price, int Volume, BidAsk BidAsk);
