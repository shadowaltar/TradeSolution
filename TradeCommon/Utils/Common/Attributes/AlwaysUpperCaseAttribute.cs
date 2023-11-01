namespace Common.Attributes;

public class AlwaysUpperCaseAttribute : AutoCorrectAttribute
{
    public bool IsNullOk { get; set; } = false;
    public override object? AutoCorrect(object? value)
    {
        return value == null ? IsNullOk : value is string s ? s.ToUpperInvariant() : value;
    }
}
