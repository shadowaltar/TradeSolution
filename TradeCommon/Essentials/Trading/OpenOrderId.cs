using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Essentials.Trading;

public record OpenOrderId
{
    public long OrderId { get; set; }
    public int SecurityId { get; set; }
    public SecurityType SecurityType { get; set; }

    public OpenOrderId() { }
}
