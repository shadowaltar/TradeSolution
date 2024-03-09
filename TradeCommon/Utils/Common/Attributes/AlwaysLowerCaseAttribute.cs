namespace Common.Attributes;

public class AlwaysLowerCaseAttribute : AutoCorrectAttribute
{
    public override object? AutoCorrect(object? value)
    {
        return value is string s ? s.ToLowerTrimmed() : value;
    }
}
