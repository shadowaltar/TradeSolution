using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Runtime;

public interface ISecurityRelatedEntry
{
    Security Security { get; set; }
    int SecurityId { get; set; }
    string SecurityCode { get; set; }
}
