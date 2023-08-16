namespace Common;
public static class Assertion
{
    public static void ShallNever(bool condition)
    {
        if (condition)
            throw new InvalidOperationException();
    }

    public static void Shall(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException();
    }
}
