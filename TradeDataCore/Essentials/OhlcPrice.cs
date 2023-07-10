namespace TradeDataCore.Essentials;
public record OhlcPrice(decimal Open, decimal High, decimal Low, decimal Close, decimal Volume, DateTime Start);
public record ExtendedOhlcPrice(string Code, string Exchange, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume, string Interval, DateTime Start);