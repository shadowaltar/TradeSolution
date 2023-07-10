using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace TradeDataCore.Utils;

public static class ReflectionUtils
{
    /// <summary>
    /// Cached untyped getters. Key is the object type (which holds the property getters).
    /// </summary>
    private static readonly Dictionary<Type, object> _getterMaps = new();

    /// <summary>
    /// Cached untyped setters. Key is the object type (which holds the property setters).
    /// </summary>
    private static readonly Dictionary<Type, object> _setterMaps = new();

    private static readonly Dictionary<string, Type?> _typesWithNameOnly = new();

    public static void SetPropertyValue(this object target, Type type, string propertyName, object? value)
    {
        var prop = type.GetProperty(propertyName);
        prop?.SetValue(target, value);
    }

    public static object? GetPropertyValue(this object target, Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName);
        return prop?.GetValue(target);
    }

    public static Func<T, TReturn>? BuildTypedGetter<T, TReturn>(PropertyInfo propertyInfo)
    {
        var getMethod = propertyInfo.GetGetMethod();
        if (getMethod == null)
            return null;
        return (Func<T, TReturn>)Delegate.CreateDelegate(typeof(Func<T, TReturn>), getMethod);
    }

    public static Action<T, TProperty>? BuildTypedSetter<T, TProperty>(PropertyInfo propertyInfo)
    {
        var setMethod = propertyInfo.GetGetMethod();
        if (setMethod == null)
            return null;
        return (Action<T, TProperty>)Delegate.CreateDelegate(typeof(Action<T, TProperty>), setMethod);
    }

    public static Action<T, object?>? BuildUntypedSetter<T>(PropertyInfo propertyInfo)
    {
        var targetType = propertyInfo.DeclaringType;
        var setMethod = propertyInfo.GetSetMethod();
        if (setMethod == null)
            return null;

        var exTarget = Expression.Parameter(targetType!, "t");
        var exValue = Expression.Parameter(typeof(object), "p");
        var exBody = Expression.Call(exTarget, setMethod, Expression.Convert(exValue, propertyInfo.PropertyType));
        var lambda = Expression.Lambda<Action<T, object?>>(exBody, exTarget, exValue);
        return lambda.Compile();
    }

    public static List<(string name, Type type, Action<T, object> setter)> BuildUntypedSetters<T>()
    {
        var properties = typeof(T).GetProperties();
        List<(string name, Type type, Action<T, object> setter)> tuples = new();
        foreach (var property in properties)
        {
            var setter = BuildUntypedSetter<T>(property);
            if (setter == null)
                continue;

            var type = property.PropertyType;
            var name = property.Name;
            tuples.Add((name, type, setter));
        }
        return tuples;
    }

    public static Func<T, object> BuildUntypedGetter<T>(PropertyInfo propertyInfo)
    {
        var targetType = propertyInfo.DeclaringType;
        var methodInfo = propertyInfo.GetGetMethod();
        //var returnType = methodInfo.ReturnType;
        var exTarget = Expression.Parameter(targetType, "t");
        var exBody = Expression.Call(exTarget, methodInfo);
        var exBody2 = Expression.Convert(exBody, typeof(object));

        var lambda = Expression.Lambda<Func<T, object>>(exBody2, exTarget);

        var action = lambda.Compile();
        return action;
    }

    public static List<(string name, Type type, Func<T, object> getter)> BuildUntypedGetters<T>()
    {
        var properties = typeof(T).GetProperties();
        List<(string name, Type type, Func<T, object> getter)> tuples = new();
        foreach (var property in properties)
        {
            var getter = BuildUntypedGetter<T>(property);
            if (getter == null)
                continue;

            var type = property.PropertyType;
            var name = property.Name;
            tuples.Add((name, type, getter));
        }
        return tuples;
    }

    public static DataTable BuildDataTable<T>()
    {
        var t = typeof(T);
        var table = new DataTable();
        var propMap = t.GetProperties();
        foreach (var property in propMap)
        {
            var type = property.PropertyType;
            table.Columns.Add(new DataColumn(property.Name, type));
        }
        return table;
    }

    public static DataTable AddDataRow<T>(DataTable table, T entry) where T : notnull
    {
        var t = typeof(T);
        var map = GetGetterMap<T>();
        var tableRow = table.NewRow();

        foreach (var (name, getter) in map)
        {
            tableRow[name] = getter(entry);
        }
        table.Rows.Add(tableRow);
        return table;
    }

    public static Dictionary<string, Func<T, object>> GetGetterMap<T>()
    {
        var t = typeof(T);
        if (!_getterMaps.TryGetValue(t, out var result))
        {
            var properties = t.GetProperties();
            var map = new Dictionary<string, Func<T, object>>();
            foreach (var property in properties)
            {
                // must exclude the static ones
                if (property.GetGetMethod()?.IsStatic ?? false)
                    continue;
                map[property.Name] = BuildUntypedGetter<T>(property);
            }
            result = map;
            _getterMaps[t] = result;
        }
        return (Dictionary<string, Func<T, object>>)result;
    }

    public static Dictionary<string, Action<T, object?>> GetSetterMap<T>()
    {
        var t = typeof(T);
        if (!_setterMaps.TryGetValue(t, out var result))
        {
            var properties = t.GetProperties();
            var map = new Dictionary<string, Action<T, object?>>();
            foreach (var property in properties)
            {
                // must exclude the static ones
                if (property.GetGetMethod()?.IsStatic ?? false)
                    continue;
                var setter = BuildUntypedSetter<T>(property);
                if (setter != null)
                    map[property.Name] = setter;
            }
            result = map;
            _setterMaps[t] = result;
        }
        return (Dictionary<string, Action<T, object?>>)result;
    }

    /// <summary>
    /// Super-simplified type name to type mapping. It will only generate the first appearance of type
    /// if multiple ones have the same name but different namespaces.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type? SearchType(string typeName, string namespaceHint = "")
    {
        if (typeName.IsBlank()) throw new ArgumentNullException(nameof(typeName));

        if (_typesWithNameOnly.TryGetValue(typeName, out var result))
            return result;

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type t in a.GetTypes())
            {
                if (!namespaceHint.IsBlank() && t.Namespace != namespaceHint)
                {
                    continue;
                }
                if (t.Name == typeName)
                {
                    _typesWithNameOnly[typeName] = t;
                    return t;
                }
            }
        }
        _typesWithNameOnly[typeName] = null;
        return null;
    }
}
