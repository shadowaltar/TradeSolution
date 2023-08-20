using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Common;
public static class CollectionExtensions
{
    /// <summary>
    /// Get value by a key from the dictionary. If no matching key,
    /// create a new instance and save into the dictionary using the given key.
    /// </summary>
    /// <typeparam name="TK"></typeparam>
    /// <typeparam name="TV"></typeparam>
    /// <param name="map"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static TV GetOrCreate<TK, TV>(this Dictionary<TK, TV> map, TK key)
        where TK : notnull
        where TV : new()
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (map.TryGetValue(key, out var value))
            return value;
        value = new TV();
        map[key] = value;
        return value;
    }

    /// <summary>
    /// Returns true if <paramref name="collection"/> is null or empty.
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)][AllowNull] this ICollection<T> collection) => collection == null || collection.Count == 0;

    public static void AddRange<T>(this Collection<T> collection, IEnumerable<T> values)
    {
        foreach (var v in values)
            collection.Add(v);
    }

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

    public static TV GetOrCreate<T, TV>(this IDictionary<T, TV> dictionary, T key) where TV : new() where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = new TV();
            dictionary[key] = value;
        }
        return value;
    }

    public static Dictionary<T, TV> ShallowCopy<T, TV>(this IDictionary<T, TV> dictionary) where T : notnull
    {
        return dictionary.ToDictionary(p => p.Key, p => p.Value);
    }
}
