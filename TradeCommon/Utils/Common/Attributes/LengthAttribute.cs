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
            return stringValue.Length >= MinLength && stringValue.Length <= MaxLength;
        }
        else if (value is ICollection collection)
        {
            return collection.Count >= MinLength && collection.Count <= MaxLength;
        }
        else if (value is Array a)
        {
            return a.Length >= MinLength && a.Length <= MaxLength;
        }
        return false;
    }
}

public class NotBlank : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value != null && value is string stringValue && !stringValue.IsBlank();
    }
}
