namespace Common.Attributes;

public class AlwaysUpperCaseAttribute : AutoCorrectAttribute
{
    public bool IsNullOk { get; set; } = false;
    public override object? AutoCorrect(object? value)
    {
        if (value == null) return IsNullOk;
        if (value is string s) { return s.ToUpperInvariant(); }
        return value;
    }
}
