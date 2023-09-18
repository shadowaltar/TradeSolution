using Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace TradeCommon.Utils.Common.Attributes;
public class AttributeMetaInfo
{
    public Dictionary<string, List<ValidationAttribute>> Validations { get; } = new();
    public Dictionary<string, AutoCorrectAttribute> AutoCorrections { get; } = new();
    public HashSet<string> DatabaseIgnoredPropertyNames { get; } = new();
    public HashSet<string> AsJsonPropertyNames { get; } = new();

    public bool IsAsJson(string name)
    {
        return AsJsonPropertyNames.Contains(name);
    }

    public bool IsDatabaseIgnored(string name)
    {
        return DatabaseIgnoredPropertyNames.Contains(name);
    }
}
