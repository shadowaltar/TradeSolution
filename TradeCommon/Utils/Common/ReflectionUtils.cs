using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Common;
public static class ReflectionUtils
{
    /// <summary>
    /// Cached untyped getters. Key is the object type (which holds the property getters).
    /// </summary>
    private static readonly Dictionary<Type, object> _typeToValueGetters = new();

    /// <summary>
    /// Cached untyped setters. Key is the object type (which holds the property setters).
    /// </summary>
    private static readonly Dictionary<Type, object> _typeToValueSetters = new();

    /// <summary>
    /// Cache of types which the key is its type name (not fully qualified name).
    /// </summary>
    private static readonly Dictionary<string, Type?> _nameOnlyTypeToTypes = new();

    /// <summary>
    /// Cache of property info for a given type. Property info are keyed by its name.
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _typeToPropertyInfoMap = new();

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
        return getMethod == null ? null : (Func<T, TReturn>)Delegate.CreateDelegate(typeof(Func<T, TReturn>), getMethod);
    }

    public static Action<T, TProperty>? BuildTypedSetter<T, TProperty>(PropertyInfo propertyInfo)
    {
        var setMethod = propertyInfo.GetGetMethod();
        return setMethod == null ? null : (Action<T, TProperty>)Delegate.CreateDelegate(typeof(Action<T, TProperty>), setMethod);
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
        var vg = GetValueGetter<T>();
        var tableRow = table.NewRow();

        foreach (var (name, value) in vg.GetAllValues(entry))
        {
            tableRow[name] = value;
        }
        table.Rows.Add(tableRow);
        return table;
    }

    public static ValueGetter<T> GetValueGetter<T>(BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        var t = typeof(T);
        if (!_typeToValueGetters.TryGetValue(t, out var vg))
        {
            var properties = GetPropertyToName(t, flags);
            vg = new ValueGetter<T>(properties.Values);
            _typeToValueGetters[t] = vg;
        }
        return (ValueGetter<T>)vg;
    }


    public static ValueSetter<T> GetValueSetter<T>(BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        var t = typeof(T);
        if (!_typeToValueGetters.TryGetValue(t, out var vg))
        {
            var properties = GetPropertyToName(t, flags).Values;
            vg = new ValueSetter<T>(properties);
            _typeToValueGetters[t] = vg;
        }
        return (ValueSetter<T>)vg;
    }

    /// <summary>
    /// Super-simplified type name to type mapping. It will only generate the first appearance of type
    /// if multiple ones have the same name but different namespaces.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type? SearchType(string? typeName, string namespaceHint = "")
    {
        if (typeName.IsBlank()) throw new ArgumentNullException(nameof(typeName));

        if (_nameOnlyTypeToTypes.TryGetValue(typeName, out var result))
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
                    _nameOnlyTypeToTypes[typeName] = t;
                    return t;
                }
            }
        }
        _nameOnlyTypeToTypes[typeName] = null;
        return null;
    }

    /// <summary>
    /// Gets a dictionary matching property name to property info, which are those properties in the specific <paramref name="type"/> instance.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static Dictionary<string, PropertyInfo> GetPropertyToName(Type type, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        if (_typeToPropertyInfoMap.TryGetValue(type, out var result))
        {
            return result;
        }
        result = type.GetProperties(flags).ToDictionary(p => p.Name, p => p);
        _typeToPropertyInfoMap[type] = result;
        return result;
    }
}

public class ValueGetter<T>
{
    private readonly Dictionary<string, Func<T, object>> _getters = new();
    private readonly Dictionary<string, Type> _getterPropertyTypes = new();

    public ValueGetter(IEnumerable<PropertyInfo> properties)
    {
        foreach (var property in properties)
        {
            // must exclude the static ones
            if (property.GetGetMethod()?.IsStatic ?? false)
                continue;
            _getters[property.Name] = ReflectionUtils.BuildUntypedGetter<T>(property);
            _getterPropertyTypes[property.Name] = property.PropertyType;
        }
    }

    public object? Get(T targetObject, string propertyName)
    {
        return _getters[propertyName].Invoke(targetObject);
    }

    public (object?, Type) GetTypeAndValue(T targetObject, string propertyName)
    {
        return (_getters[propertyName].Invoke(targetObject), _getterPropertyTypes[propertyName]);
    }

    public IEnumerable<(string, object)> GetAllValues(T entry)
    {
        foreach (var (name, getter) in _getters)
        {
            yield return (name, getter(entry));
        }
    }
}

public class ValueSetter<T>
{
    private readonly Dictionary<string, Action<T, object>> _setters = new();

    public ValueSetter(Dictionary<string, PropertyInfo>.ValueCollection properties)
    {
        foreach (var property in properties)
        {
            // must exclude the static ones
            if (property.GetGetMethod()?.IsStatic ?? false)
                continue;
            var setter = ReflectionUtils.BuildUntypedSetter<T>(property);
            if (setter != null)
                _setters[property.Name] = setter;
        }
    }

    public void Set(T targetObject, string propertyName, object? value)
    {
        _setters[propertyName].Invoke(targetObject, value);
    }

    public IEnumerable<string> GetFieldNames()
    {
        return _setters.Keys;
    }
}