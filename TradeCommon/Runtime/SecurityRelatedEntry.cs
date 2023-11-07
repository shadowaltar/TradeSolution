using Common;
using Common.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Runtime;

public record SecurityRelatedEntry
{
    [DatabaseIgnore, JsonIgnore]
    public Security Security { get; set; }

    [NotNull, Positive, Column(Order = 1)]
    public int SecurityId { get; set; } = 0;

    [DatabaseIgnore]
    public string SecurityCode { get; set; } = "";

    public bool IsSecurityInvalid()
    {
        return Security == null || SecurityId <= 0 || SecurityCode.IsBlank();
    }
}
