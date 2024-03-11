using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;
public class AttributeMetaInfo
{
    public Dictionary<string, List<ValidationAttribute>> Validations { get; } = [];
    public Dictionary<string, AutoCorrectAttribute> AutoCorrections { get; } = [];
    public HashSet<string> DatabaseIgnoredPropertyNames { get; } = [];
    public HashSet<string> AsJsonPropertyNames { get; } = [];
    public HashSet<string> PrimaryUniqueKey { get; } = [];
    public List<HashSet<string>> AllUniqueKeys { get; } = [];

    public bool IsAsJson(string name)
    {
        return AsJsonPropertyNames.Contains(name);
    }

    public bool IsDatabaseIgnored(string name)
    {
        return DatabaseIgnoredPropertyNames.Contains(name);
    }
}
