using System.Diagnostics.CodeAnalysis;

namespace Common.Attributes;

/// <summary>
/// Defines unique constraint / clause for a class.
/// If there are multiple, the 1st one is the primary one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class UniqueAttribute : Attribute, IStorageRelatedAttribute, IFieldNamesAttribute
{
    /// <summary>
    /// The table field names for a unique clause
    /// </summary>
    public string[] FieldNames { get; }

    public UniqueAttribute(params string[] names)
    {
        FieldNames = names;
    }

    public override string? ToString()
    {
        return "UNIQUE: " + string.Join(",", FieldNames);
    }

    public bool Equals(UniqueAttribute? other)
    {
        if (other == null) return false;
        foreach (var fieldName in FieldNames)
        {
            if (!other.FieldNames.Contains(fieldName)) return false;
        }
        foreach (var fieldName in other.FieldNames)
        {
            if (!FieldNames.Contains(fieldName)) return false;
        }
        return true;
    }
}

/// <summary>
/// Defines index for a class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class IndexAttribute : Attribute, IFieldNamesAttribute
{
    /// <summary>
    /// The table field names for a unique clause
    /// </summary>
    public string[] FieldNames { get; }

    public IndexAttribute(params string[] names)
    {
        FieldNames = names;
    }

    public override string? ToString()
    {
        return "INDEX: " + string.Join(",", FieldNames);
    }

    public bool Equals(IndexAttribute? other)
    {
        if (other == null) return false;
        foreach (var fieldName in FieldNames)
        {
            if (!other.FieldNames.Contains(fieldName)) return false;
        }
        foreach (var fieldName in other.FieldNames)
        {
            if (!FieldNames.Contains(fieldName)) return false;
        }
        return true;
    }
}

public class FieldNameEqualityComparer : IEqualityComparer<IFieldNamesAttribute>
{
    public bool Equals(IFieldNamesAttribute? x, IFieldNamesAttribute? y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if (x is null || y is null)
            return false;

        foreach (var fieldName in x.FieldNames)
        {
            if (!y.FieldNames.Contains(fieldName)) return false;
        }
        foreach (var fieldName in y.FieldNames)
        {
            if (!x.FieldNames.Contains(fieldName)) return false;
        }
        return true;
    }

    public int GetHashCode([DisallowNull] IFieldNamesAttribute obj)
    {
        return obj.FieldNames.Hash();
    }
}