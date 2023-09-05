namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class UniqueAttribute : Attribute
{
    public string[] FieldNames { get; }

    public UniqueAttribute(params string[] names)
    {
        FieldNames = names;
    }
}
