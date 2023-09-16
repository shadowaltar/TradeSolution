namespace Common.Attributes;

/// <summary>
/// Indicates a property when being insert into database,
/// values in it will be auto-incremented; when being updated,
/// this value will not be auto-incremented.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AutoIncrementOnInsertAttribute : DatabaseIgnoreAttribute, IStorageRelatedAttribute
{
    public AutoIncrementOnInsertAttribute() : base(true, true, false)
    {
    }
}
