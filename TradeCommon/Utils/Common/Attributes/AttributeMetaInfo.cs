using Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace TradeCommon.Utils.Common.Attributes;
public class AttributeMetaInfo
{
    public Dictionary<string, List<ValidationAttribute>> Validations { get; } = new();
    public Dictionary<string, AutoCorrectAttribute> AutoCorrections { get; } = new();
    public HashSet<string> DatabaseIgnoredPropertyNames { get; } = new();
}
