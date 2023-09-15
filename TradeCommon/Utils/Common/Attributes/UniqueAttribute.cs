namespace Common.Attributes;

/// <summary>
/// Defines unique constraint / clause for a class.
/// If there are multiple, the 1st one is the primary one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class UniqueAttribute : Attribute
{
    /// <summary>
    /// The table field names for a unique clause
    /// </summary>
    public string[] FieldNames { get; }

    public UniqueAttribute(params string[] names)
    {
        FieldNames = names;
    }
}

/// <summary>
/// Defines index for a class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class IndexAttribute : Attribute
{
    /// <summary>
    /// The table field names for a unique clause
    /// </summary>
    public string[] FieldNames { get; }

    public IndexAttribute(params string[] names)
    {
        FieldNames = names;
    }
}
