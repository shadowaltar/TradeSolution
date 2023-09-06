using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Common;
public static class CollectionExtensions
{
    private static readonly Random random = new(DateTime.Now.Millisecond);

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

    public static void AddRange<Tk, Tv>(this IDictionary<Tk, Tv>? collection, IEnumerable<KeyValuePair<Tk, Tv>>? values)
    {
        if (collection == null || values == null) return;
        foreach (var v in values)
            collection.Add(v);
    }

    public static void AddRange<T, Tk, Tv>(this IDictionary<Tk, Tv>? collection, IEnumerable<T>? items, Func<T, Tk> keySelector, Func<T, Tv> valueSelector)
    {
        if (collection == null || items == null) return;
        foreach (var item in items)
            collection[keySelector(item)] = valueSelector(item);
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

    public static TV? GetOrDefault<T, TV>(this IDictionary<T, TV?> dictionary, T key, TV? defaultValue = default) where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = defaultValue;
            dictionary[key] = value;
        }
        return value;
    }

    public static Dictionary<T, TV> ShallowCopy<T, TV>(this IDictionary<T, TV> dictionary) where T : notnull
    {
        return dictionary.ToDictionary(p => p.Key, p => p.Value);
    }

    public static bool ThreadSafeTryGet<T, TV>(this Dictionary<T, TV> dictionary, T key, out TV y) where T : notnull
    {
        lock (dictionary)
        {
            var r = dictionary.TryGetValue(key, out y);
            return r;
        }
    }

    public static void ThreadSafeSet<T, TV>(this Dictionary<T, TV> dictionary, T key, TV y) where T : notnull
    {
        lock (dictionary)
        {
            dictionary[key] = y;
        }
    }

    public static IEnumerable<T> RandomElements<T>(this IList<T> list, int count)
    {
        for (var i = 0; i < count; i++)
            yield return RandomElement(list);
    }

    public static T RandomElement<T>(this IList<T> list)
    {
        return list[random.Next(list.Count)];
    }

    public static IEnumerable<T> RandomElements<T>(this T[] array, int count)
    {
        for (var i = 0; i < count; i++)
            yield return RandomElement(array);
    }

    public static T RandomElement<T>(this T[] array)
    {
        return array[random.Next(array.Length)];
    }
}
