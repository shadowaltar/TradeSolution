namespace Common.Attributes;
public class DatabaseIgnoreAttribute : Attribute
{
    public DatabaseIgnoreAttribute(bool ignoreInsert = true, bool ignoreUpsert = true, bool ignoreSelect = true)
    {
        IgnoreInsert = ignoreInsert;
        IgnoreUpsert = ignoreUpsert;
        IgnoreSelect = ignoreSelect;
    }

    public bool IgnoreInsert { get; }
    public bool IgnoreUpsert { get; }
    public bool IgnoreSelect { get; }
}
