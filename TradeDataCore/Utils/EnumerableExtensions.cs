namespace TradeDataCore.Utils;

public static class EnumerableExtensions
{
    public static IEnumerable<List<T>> Split<T>(this IEnumerable<T> items, int bucketSize = 30)
    {
        if (bucketSize <= 0)
        {
            yield break;
        }

        var currentBucket = new List<T>(bucketSize);
        int itemIndex = 0;
        foreach (T i in items)
        {
            currentBucket.Add(i);
            itemIndex++;
            if (itemIndex != 0 && itemIndex % bucketSize == 0)
            {
                yield return currentBucket;
                currentBucket = new List<T>(bucketSize);
            }
        }

        if (currentBucket.Count > 0)
        {
            yield return currentBucket;
        }
    }
}
