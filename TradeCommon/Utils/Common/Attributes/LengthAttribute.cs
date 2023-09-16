using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class LengthAttribute : ValidationAttribute, IStorageRelatedAttribute
{
    public int MinLength { get; set; }
    public int MaxLength { get; set; }

    public LengthAttribute(int minLength = int.MinValue, int maxLength = int.MaxValue)
    {
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public override bool IsValid(object? value)
    {
        if (value == null) return false;
        if (value is string stringValue)
        {
            if (stringValue.Length < MinLength) return false;
            if (stringValue.Length > MaxLength) return false;
            return true;
        }
        else if (value is ICollection collection)
        {
            if (collection.Count < MinLength) return false;
            if (collection.Count > MaxLength) return false;
            return true;
        }
        else if (value is Array a)
        {
            if (a.Length < MinLength) return false;
            if (a.Length > MaxLength) return false;
            return true;
        }
        return false;
    }
}

public class NotBlank : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null) return false;
        if (value is string stringValue)
        {
            return !stringValue.IsBlank();
        }
        return false;
    }
}
