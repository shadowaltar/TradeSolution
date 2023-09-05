using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NotUnknownAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null || !value.GetType().IsEnum) return false;

        var str = value.ToString();
        if (str == "UNKNOWN" || str == "Unknown") return false;
        return true;
    }
}