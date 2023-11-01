using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NotUnknownAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null) return false;
        var str = value.ToString();
        return str is not "UNKNOWN" and not "Unknown";
    }
}