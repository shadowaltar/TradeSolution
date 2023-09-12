using System.ComponentModel.DataAnnotations;

namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NotNegativeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is int)
        {
            return (int)value >= 0;
        }
        if (value is long)
        {
            return (long)value >= 0;
        }
        else if (value is float or double)
        {
            return (double)value >= 0;
        }
        else if (value is decimal decimalValue)
        {
            return decimalValue >= 0;
        }
        if (value is short)
        {
            return (short)value >= 0;
        }
        if (value is byte)
        {
            return (byte)value >= 0;
        }
        return false;
    }
}