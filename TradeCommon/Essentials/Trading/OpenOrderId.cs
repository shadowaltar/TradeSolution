using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Essentials.Trading;

public record OpenOrderId
{
    public long OrderId { get; set; } = 0;
    public int SecurityId { get; set; } = 0;
    public SecurityType SecurityType { get; set; }

    public OpenOrderId() { }
}
