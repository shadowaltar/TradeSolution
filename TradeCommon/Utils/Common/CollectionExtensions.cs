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
    public static Tv GetOrCreate<Tk, Tv>(this IDictionary<Tk, Tv> map, Tk key, Func<Tv> createAction, Action<Tk, Tv>? afterCreated = null)
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
    public static Tv GetOrCreate<Tk, Tv>(this IDictionary<Tk, Tv> map, Tk key, Action<Tk, Tv>? afterCreated = null)
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
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)][AllowNull] this ICollection<T> collection)
    {
        return collection == null || collection.Count == 0;
    }

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

    /// <summary>
    /// Append values to an existing collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public static void AddRange<T>(this IList<T> collection, params T[] values)
    {
        if (values.IsNullOrEmpty()) return;
        foreach (var item in values)
        {
            collection.Add(item);
        }
    }

    public static void ClearAddRange<T>(this List<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        collection.AddRange(values);
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

    public static void Move<T>(this IList<T> collection, T item, int index)
    {
        if (!collection.Remove(item))
            throw new InvalidOperationException("Collection does not contain the specific item.");

        collection.Insert(index, item);
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

    public static Tv? GetOrDefault<T, Tv>(this IReadOnlyDictionary<T, Tv> dictionary, T key, Tv? defaultValue = default) where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = defaultValue;
        }
        return value;
    }

    public static Tv? GetOrDefault<T, Tv>(this IDictionary<T, Tv> dictionary, T key, Tv? defaultValue = default) where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = defaultValue;
        }
        return value;
    }

    public static Tv? GetOrDefault<T, Tv>(this Dictionary<T, Tv> dictionary, T key, Tv? defaultValue = default) where T : notnull
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (!dictionary.TryGetValue(key, out var value))
        {
            value = defaultValue;
        }
        return value;
    }

    public static (List<TV>, IDictionary<TK, TV>, List<TK>) FindDifferences<TK, TV>(
        IDictionary<TK, TV> primary, IDictionary<TK, TV> secondary, Func<TV, TV, bool>? comparisonFunc = null)
    {
        var toCreate = new List<TV>();
        var toUpdate = new Dictionary<TK, TV>();
        var toDelete = new List<TK>();
        foreach (var (id, first) in primary)
        {
            if (secondary.TryGetValue(id, out var second))
            {
                if (comparisonFunc != null)
                {
                    if (!comparisonFunc.Invoke(first, second))
                        toUpdate[id] = first;
                }
                else if (!second.Equals(first))
                    toUpdate[id] = first;
            }
            else
            {
                toCreate.Add(first);
            }
        }
        foreach (var (id, second) in secondary)
        {
            if (primary.TryGetValue(id, out var first))
            {
                if (comparisonFunc != null)
                {
                    if (!comparisonFunc.Invoke(first, second))
                        toUpdate[id] = first;
                }
                else if (!second.Equals(first))
                    toUpdate[id] = first;
            }
            else
            {
                toDelete.Add(id);
            }
        }
        return (toCreate, toUpdate, toDelete);
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

    public static IDictionary<T, Tv> ShallowCopy<T, Tv>(this IDictionary<T, Tv> dictionary) where T : notnull
    {
        return dictionary.ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>
    /// Thread-safe version of <see cref="GetOrDefault{T, Tv}(IReadOnlyDictionary{T, Tv}, T, Tv?)"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static Tv? ThreadSafeGet<T, Tv>(this IDictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            return dictionary!.GetOrDefault(key);
        }
    }

    /// <summary>
    /// Thread-safe version of <see cref="GetOrCreate{T, Tv}(IDictionary{T, Tv}, T)"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static Tv ThreadSafeGetOrCreate<T, Tv>(this IDictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
          where Tv : new()
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            return dictionary.GetOrCreate(key);
        }
    }

    public static bool ThreadSafeContains<T, Tv>(this IDictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            return dictionary!.ContainsKey(key);
        }
    }

    public static void ThreadSafeClear<T, Tv>(this IDictionary<T, Tv> dictionary, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            dictionary!.Clear();
        }
    }

    /// <summary>
    /// Remove an item by its key from dictionary. Returns false if it is removed and it existed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static bool ThreadSafeRemove<T, Tv>(this IDictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            return dictionary!.Remove(key);
        }
    }

    /// <summary>
    /// Returns the value by the key from the dictionary,
    /// and also removes it.
    /// Returns null if it does not exist.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static Tv? ThreadSafeGetAndRemove<T, Tv>(this IDictionary<T, Tv> dictionary, T key, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            var value = dictionary.GetOrDefault(key, default);
            dictionary.Remove(key);
            return value;
        }
    }

    /// <summary>
    /// Checks if the dictionary contains a key and returns true if yes.
    /// Returns false if it does not contain the key, and the value will be list into it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static bool ThreadSafeContainsOrSet<T, Tv>(this IDictionary<T, Tv> dictionary, T key, Tv value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            if (dictionary.ContainsKey(key))
                return true;
            dictionary[key] = value;
            return false;
        }
    }

    public static T? ThreadSafeFirst<T>(this IEnumerable<T> collection, Func<T, bool> selector, object? @lock = null)
    {
        object lockObject = @lock ?? collection;
        lock (lockObject)
        {
            return collection.FirstOrDefault(selector);
        }
    }

    /// <summary>
    /// Try to get an item by a key.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Tv"></typeparam>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="y"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static bool ThreadSafeTryGet<T, Tv>(this IDictionary<T, Tv> dictionary, T key, out Tv y, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            var r = dictionary.TryGetValue(key, out y);
            return r;
        }
    }

    public static void ThreadSafeSet<T, Tv>(this IDictionary<T, Tv> dictionary, T key, Tv y, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            dictionary[key] = y;
        }
    }

    public static List<Tv> ThreadSafeValues<T, Tv>(this IDictionary<T, Tv> dictionary, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? dictionary;
        lock (lockObject)
        {
            return dictionary.Values.ToList();
        }
    }

    public static void ThreadSafeAdd<T>(this List<T> list, T value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? list;
        lock (lockObject)
        {
            list.Add(value);
        }
    }

    public static bool ThreadSafeAdd<T>(this HashSet<T> set, T value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? set;
        lock (lockObject)
        {
            return set.Add(value);
        }
    }

    public static bool ThreadSafeContains<T>(this HashSet<T> set, T value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? set;
        lock (lockObject)
        {
            return set.Contains(value);
        }
    }

    /// <summary>
    /// [Thread-safe] check if list contains given value. If yes returns true,
    /// otherwise returns false and also add the value into the list.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="set"></param>
    /// <param name="value"></param>
    /// <param name="lock"></param>
    /// <returns></returns>
    public static bool ThreadSafeContainsElseAdd<T>(this HashSet<T> set, T value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? set;
        lock (lockObject)
        {
            var r = set.Contains(value);
            if (r) return true;
            set.Add(value);
            return false;
        }
    }

    public static bool ThreadSafeRemove<T>(this HashSet<T> set, T value, object? @lock = null) where T : notnull
    {
        object lockObject = @lock ?? set;
        lock (lockObject)
        {
            return set.Remove(value);
        }
    }

    public static IDictionary<TKey, TElement> SafeToDictionary<TSource, TKey, TElement>(this List<TSource> source,
                                                                                       Func<TSource, TKey> keySelector,
                                                                                       Func<TSource, TElement> elementSelector,
                                                                                       out List<TSource>? failedItems)
        where TKey : notnull
    {
        failedItems = null;
        var results = new Dictionary<TKey, TElement>(source.Count);
        foreach (var item in source)
        {
            var k = keySelector(item);
            var v = elementSelector(item);

            if (!results.TryAdd(k, v))
            {
                failedItems ??= new List<TSource>();
                failedItems.Add(item);
            }
        }
        return results;
    }

    public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            set.Add(value);
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

    public static int Hash<T>(this IList<T> list)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));

        int hash = 17;
        unchecked
        {
            for (int i = 0; i < list.Count; i++)
            {
                var value = list[i];
                if (value == null) continue;
                hash = (31 * hash) + value.GetHashCode();
            }
        }
        return hash;
    }

    public static IList<T> Clone<T>(this IList<T> list) where T : ICloneable
    {
        var result = new List<T>(list.Count);
        foreach (var item in list)
        {
            result.Add((T)item.Clone());
        }
        return result;
    }
}
