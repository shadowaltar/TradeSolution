using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Runtime;

public record SecurityRelatedEntry
{
    [DatabaseIgnore]
    public Security Security { get; set; }

    [NotNull, Positive]
    public int SecurityId { get; set; } = 0;

    [DatabaseIgnore]
    public string SecurityCode { get; set; } = "";


    [DatabaseIgnore]
    public bool IsSecurityInvalid()
    {
        return Security == null || SecurityId <= 0 || SecurityCode.IsBlank();
    }
}
