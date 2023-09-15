using Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace TradeCommon.Utils.Common.Attributes;
public class AttributeMetaInfo
{
    public Dictionary<string, List<ValidationAttribute>> ValidationProperties { get; } = new();
    public Dictionary<string, AutoCorrectAttribute> AutoCorrectProperties { get; } = new();
}
