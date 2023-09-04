namespace TradeCommon.Utils.Attributes;

public class UniqueAttribute : Attribute
{
    public string[] FieldNames { get; }

    public UniqueAttribute(params string[] names)
    {
        FieldNames = names;
    }
}
