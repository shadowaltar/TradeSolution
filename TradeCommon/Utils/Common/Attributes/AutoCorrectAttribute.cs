namespace Common.Attributes;

public abstract class AutoCorrectAttribute : Attribute
{
    public abstract object? AutoCorrect(object? value);
}
