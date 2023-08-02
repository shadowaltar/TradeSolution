namespace Common;
public static class NumericExtensions
{
    public static decimal? NullIfZero(this decimal value)
    {
        return value == 0 ? null : value;
    }

    public static long ReverseDigits(this long value)
    {
        long reverse = 0;
        long rem;
        while (value != 0)
        {
            rem = value % 10;
            reverse = reverse * 10 + rem;
            value /= 10;
        }
        return reverse;
    }
}
