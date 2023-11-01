using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;
public class AttributeMetaInfo
{
    public Dictionary<string, List<ValidationAttribute>> Validations { get; } = new();
    public Dictionary<string, AutoCorrectAttribute> AutoCorrections { get; } = new();
    public HashSet<string> DatabaseIgnoredPropertyNames { get; } = new();
    public HashSet<string> AsJsonPropertyNames { get; } = new();
    public HashSet<string> PrimaryUniqueKey { get; } = new();
    public List<HashSet<string>> AllUniqueKeys { get; } = new();

    public bool IsAsJson(string name)
    {
        return AsJsonPropertyNames.Contains(name);
    }

    public bool IsDatabaseIgnored(string name)
    {
        return DatabaseIgnoredPropertyNames.Contains(name);
    }
}
