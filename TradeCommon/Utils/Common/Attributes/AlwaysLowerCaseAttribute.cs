namespace Common.Attributes;

public class AlwaysLowerCaseAttribute : AutoCorrectAttribute
{
    public override object? AutoCorrect(object? value)
    {
        if (value is string s) { return s.ToLowerInvariant(); }
        return value;
    }
}
