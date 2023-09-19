using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Common;
public static class CollectionExtensions
{
    private static readonly Random _random = new(DateTime.Now.Millisecond);

    /// <summary>
    /// Get value by a key from the dictionary. If no matching key,
    /// create a new instance and save into the dictionary using the given key.
    /// </summary>
    /// <typeparam name="Tk"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="map"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static Tv GetOrCreate<Tk, Tv>(this Dictionary<Tk, Tv> map, Tk key, Func<Tv> createAction, Action<Tk, Tv>? afterCreated = null)
        where Tk : notnull
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (map.TryGetValue(key, out var value))
            return value;
        value = createAction.Invoke();
        map[key] = value;
        afterCreated?.Invoke(key, value);
        return value;
    }

    /// <summary>
    /// Get value by a key from the dictionary. If no matching key,
    /// create a new instance and save into the dictionary using the given key.
    /// </summary>
    /// <typeparam name="Tk"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="map"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static Tv GetOrCreate<Tk, Tv>(this Dictionary<Tk, Tv> map, Tk key, Action<Tk, Tv>? afterCreated = null)
        where Tv : new()
        where Tk : notnull
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (map.TryGetValue(key, out var value))
            return value;
        value = new();
        map[key] = value;
        afterCreated?.Invoke(key, value);
        return value;
    }

    /// <summary>
    /// Returns true if <paramref name="collection"/> is null or empty.
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)][AllowNull] this ICollection<T> collection) => collection == null || collection.Count == 0;

    /// <summary>
    /// Append values to an existing array.
    /// It will always return a new array instance.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public static T[] AddRange<T>(this T[] collection, ICollection<T> values)
    {
        if (values.IsNullOrEmpty()) return collection.ToArray();
        var count = collection.Length;
        var results = new T[count + values.Count];
        Array.Copy(collection, results, count);
        var i = 0;
        foreach (var item in values)
        {
            results[count + i] = item;
            i++;
        }
        return results;
    }

    public static void AddRange<T>(this Collection<T> collection, IEnumerable<T> values)
    {
        foreach (var v in values)
            collection.Add(v);
    }

    public static void AddOrAddRange<T>(this ICollection<T> collection, IEnumerable<T>? values, T? value)
    {
        if (values != null)
        {
            foreach (var v in values)
            {
                collection.Add(v);
            }
        }
        if (value != null)
            collection.Add(value);
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

    public static Tv GetOrCreate<T, Tv>(this IDictionary<T, Tv> dictionary, T key) where Tv : new() where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = new Tv();
            dictionary[key] = value;
        }
        return value;
    }

    public static Tv? GetOrDefault<T, Tv>(this IDictionary<T, Tv?> dictionary, T key, Tv? defaultValue = default) where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = defaultValue;
            dictionary[key] = value;
        }
        return value;
    }

    public static (Dictionary<Tk, Tv> primaryOnly, Dictionary<Tk, Tv> contentDiffs, Dictionary<Tk, Tv> secondaryOnly)
        FindDifferences<Tk, Tv>(this IDictionary<Tk, Tv> primary, IDictionary<Tk, Tv> secondary)
        where Tk : notnull
        where Tv : IComparable<Tv>
    {
        var primaryOnly = new Dictionary<Tk, Tv>();
        var contentDiffs = new Dictionary<Tk, Tv>();
        var secondaryOnly = new Dictionary<Tk, Tv>();
        foreach (var (id, first) in primary)
        {
            if (secondary.TryGetValue(id, out var second))
            {
                if (second.CompareTo(first) != 0)
                    contentDiffs[id] = first;
            }
            else
            {
                primaryOnly[id] = first;
            }
        }
        foreach (var (id, second) in secondary)
        {
            if (primary.TryGetValue(id, out var first))
            {
                if (second.CompareTo(first) != 0)
                    contentDiffs[id] = first;
            }
            else
            {
                secondaryOnly[id] = second;
            }
        }
        return (primaryOnly, contentDiffs, secondaryOnly);
    }

    public static (List<T>, List<T>) FindDifferences<T>(this IList<T> primary, IList<T> secondary)
        where T : IComparable<T>
    {
        var primaryOnly = new List<T>();
        var secondaryOnly = new List<T>();
        foreach (var first in primary)
        {
            if (!secondary.Contains(first))
            {
                primaryOnly.Add(first);
            }
        }
        foreach (var second in secondary)
        {
            if (!primary.Contains(second))
            {
                secondaryOnly.Add(second);
            }
        }
        return (primaryOnly, secondaryOnly);
    }

    public static Dictionary<T, Tv> ShallowCopy<T, Tv>(this IDictionary<T, Tv> dictionary) where T : notnull
    {
        return dictionary.ToDictionary(p => p.Key, p => p.Value);
    }

    public static Tv? ThreadSafeGet<T, Tv>(this Dictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock != null ? @lock : dictionary;
        lock (lockObject)
        {
            return dictionary!.GetOrDefault(key);
        }
    }

    public static bool ThreadSafeRemove<T, Tv>(this Dictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock != null ? @lock : dictionary;
        lock (lockObject)
        {
            return dictionary!.Remove(key);
        }
    }

    public static bool ThreadSafeTryGet<T, Tv>(this Dictionary<T, Tv> dictionary, T key, out Tv y, object? @lock = null) where T : notnull
    {
        object lockObject = @lock != null ? @lock : dictionary;
        lock (lockObject)
        {
            var r = dictionary.TryGetValue(key, out y);
            return r;
        }
    }

    public static void ThreadSafeSet<T, Tv>(this Dictionary<T, Tv> dictionary, T key, Tv y, object? @lock = null) where T : notnull
    {
        object lockObject = @lock != null ? @lock : dictionary;
        lock (lockObject)
        {
            dictionary[key] = y;
        }
    }

    public static List<Tv> ThreadSafeValues<T, Tv>(this Dictionary<T, Tv> dictionary, object? @lock = null) where T : notnull
    {
        object lockObject = @lock != null ? @lock : dictionary;
        lock (lockObject)
        {
            return dictionary.Values.ToList();
        }
    }

    public static IEnumerable<T> RandomElements<T>(this IList<T> list, int count)
    {
        for (var i = 0; i < count; i++)
            yield return RandomElement(list);
    }

    public static T RandomElement<T>(this IList<T> list)
    {
        return list[_random.Next(list.Count)];
    }

    public static IEnumerable<T> RandomElements<T>(this T[] array, int count)
    {
        for (var i = 0; i < count; i++)
            yield return RandomElement(array);
    }

    public static T RandomElement<T>(this T[] array)
    {
        return array[_random.Next(array.Length)];
    }
}
