using Common.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics.CodeAnalysis;
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
    private static readonly Dictionary<string, Type?> _typesByTypeName = new();

    /// <summary>
    /// Cache of types which the key is its super type.
    /// </summary>
    private static readonly Dictionary<Type, List<Type>> _typesBySuperType = new();

    /// <summary>
    /// Cache of property info for a given type. Property info are keyed by its name.
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _typeToPropertyInfoMap = new();

    /// <summary>
    /// Cache of meta info for a given type. See <see cref="ReflectionMetaInfo{T}"/> for details.
    /// </summary>
    private static readonly Dictionary<Type, object> _typeToMetaInfo = new();

    /// <summary>
    /// Cache of attribute meta info for a given type. See <see cref="AttributeMetaInfo"/> for details.
    /// </summary>
    private static readonly Dictionary<Type, AttributeMetaInfo> _typeToAttributeInfo = new();

    public static bool IsRecord(this Type type)
    {
        return type.GetMethods().Any(m => m.Name == "<Clone>$");
    }

    public static bool IsSuperOf<TSub>(this Type type)
    {
        return type.IsAssignableFrom(typeof(TSub));
    }

    public static bool IsSuperOf(this Type type, Type subType)
    {
        return type.IsAssignableFrom(subType);
    }

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
        var vg = GetValueGetter<T>();
        var tableRow = table.NewRow();
        foreach (var (name, value) in vg.GetNamesAndValues(entry))
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

            InitializeMetaInfo(t, out ReflectionMetaInfo<T> i, out AttributeMetaInfo a);

            vg = new ValueGetter<T>(properties.Values, i, a);
            _typeToValueGetters[t] = vg;
        }
        return (ValueGetter<T>)vg;
    }

    public static ValueSetter<T> GetValueSetter<T>(BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        var t = typeof(T);
        if (!_typeToValueSetters.TryGetValue(t, out var vg))
        {
            var properties = GetPropertyToName(t, flags).Values;

            InitializeMetaInfo(t, out ReflectionMetaInfo<T> i, out AttributeMetaInfo a);

            vg = new ValueSetter<T>(properties, i, a);
            _typeToValueSetters[t] = vg;
        }
        return (ValueSetter<T>)vg;
    }

    /// <summary>
    /// Search a type by its name from all loaded assemblies.
    /// It will only get the first appearance if same name types exists under different namespaces.
    /// Optionally you can specify a <paramref name="namespaceHint"/> to find the type from there only;
    /// or specify a super type of the desired types to limit the search results.
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type? SearchType(string typeName, string namespaceHint = "", Type? superTypeHint = null)
    {
        if (typeName.IsBlank()) throw new ArgumentNullException(nameof(typeName));

        if (_typesByTypeName.TryGetValue(typeName, out var result))
            return result;

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type t in a.GetTypes())
            {
                if (!namespaceHint.IsBlank() && t.Namespace != namespaceHint)
                    continue;
                if (superTypeHint != null && !superTypeHint.IsAssignableFrom(t))
                    continue;

                if (t.Name == typeName)
                {
                    _typesByTypeName[typeName] = t;
                    return t;
                }
            }
        }
        _typesByTypeName[typeName] = null;
        return null;
    }

    public static List<Type> SearchType(Type superType, string namespaceHint = "")
    {
        if (superType == null) throw new ArgumentNullException(nameof(superType));

        if (_typesBySuperType.TryGetValue(superType, out var results))
            return results;

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type t in a.GetTypes())
            {
                if (!namespaceHint.IsBlank() && t.Namespace != namespaceHint)
                    continue;

                if (superType.IsAssignableFrom(t))
                {
                    var subTypes = _typesBySuperType.GetOrCreate(superType);
                    subTypes.Add(t);
                }
            }
        }
        return _typesBySuperType.GetOrCreate(superType);
    }

    /// <summary>
    /// Gets a dictionary matching property name to property info, which are those properties in the specific <paramref name="type"/> instance.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static Dictionary<string, PropertyInfo> GetPropertyToName(Type type, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        if (_typeToPropertyInfoMap.TryGetValue(type, out var results))
        {
            return results;
        }

        // get a list of inherited classes, from base to sub
        var typeChain = type.GetBaseTypeChain().ToList();
        typeChain.Reverse();
        typeChain.Add(type);

        results = new Dictionary<string, PropertyInfo>();
        foreach (var t in typeChain)
        {
            // only in current class (no super), exclude the static and non-public getter
            var properties = t.GetProperties(flags | BindingFlags.DeclaredOnly)
                .Where(p => !(p.GetGetMethod()?.IsStatic ?? false));
            foreach (var p in properties)
            {
                results[p.Name] = p;
            }
        }
        _typeToPropertyInfoMap[type] = results;
        return results;
    }

    public static IEnumerable<Type> GetBaseTypeChain(this Type type)
    {
        var t = type;
        while (true)
        {
            var baseClass = t.BaseType;
            if (baseClass?.Name is "Object" or null)
                yield break;
            yield return baseClass;
            t = baseClass;
        }
    }

    public static List<(string name, PropertyInfo property, int index)> GetOrderedPropertyAndName(Type type, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        return type.GetProperties(flags)
            .Where(p => !(p.GetGetMethod()?.IsStatic ?? false))
            .Select((p, i) => (p.Name, p, i)).ToList();
    }

    public static AttributeMetaInfo GetAttributeInfo<T>()
    {
        var t = typeof(T);
        if (!_typeToAttributeInfo.TryGetValue(t, out var ami))
        {
            InitializeMetaInfo<T>(t, out _, out var result);
            ami = result;
        }
        return ami;
    }

    public static Assembly[] GetAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies();
    }

    public static List<Type> GetTypes(string @namespace)
    {
        var types = new List<Type>();
        var assemblies = GetAssemblies();
        foreach (var asm in assemblies)
        {
            types.AddRange(asm.GetTypes().Where(p => p.Namespace == @namespace));
        }
        return types;
    }


    private static void InitializeMetaInfo<T>(Type t, out ReflectionMetaInfo<T> rmi, out AttributeMetaInfo ami)
    {
        // get cached or initialize reflection related meta info
        if (!_typeToMetaInfo.TryGetValue(t, out var info))
        {
            rmi = new ReflectionMetaInfo<T>();
            _typeToMetaInfo[t] = rmi;

            rmi.Ordering.AddRange(GetOrderedPropertyAndName(t).ToDictionary(t => t.name, t => t.index));
        }
        else
        {
            rmi = (ReflectionMetaInfo<T>)info;
        }

        // get cached or initialize attribute related meta info
        if (!_typeToAttributeInfo.TryGetValue(t, out ami))
        {
            ami = new AttributeMetaInfo();
            _typeToAttributeInfo[t] = ami;

            var uniqueAttrs = t.GetDistinctAttributes<UniqueAttribute>().ToList();
            for (int i = 0; i < uniqueAttrs.Count; i++)
            {
                UniqueAttribute? attr = uniqueAttrs[i];
                if (i == 0)
                    ami.PrimaryUniqueKey.AddRange(attr.FieldNames);

                ami.AllUniqueKeys.Add(new HashSet<string>(attr.FieldNames));
            }

            foreach (var (name, property) in GetPropertyToName(t))
            {
                var ignoreAttr = property.GetCustomAttribute<DatabaseIgnoreAttribute>();
                if (ignoreAttr != null)
                {
                    ami.DatabaseIgnoredPropertyNames.Add(name);
                }
                var asJsonAttr = property.GetCustomAttribute<AsJsonAttribute>();
                if (asJsonAttr != null)
                {
                    ami.AsJsonPropertyNames.Add(name);
                }

                // mark the attributes
                foreach (var attribute in Validator.ValidationAttributes)
                {
                    var attr = property.GetCustomAttribute(attribute);
                    if (attr is ValidationAttribute va)
                    {
                        var attrs = ami.Validations.GetOrCreate(property.Name);
                        attrs.Add(va);
                    }
                }
                foreach (var attribute in Validator.AutoCorrectAttributes)
                {
                    var attr = property.GetCustomAttribute(attribute);
                    if (attr is AutoCorrectAttribute aa)
                    {
                        ami.AutoCorrections[property.Name] = aa;
                    }
                }
            }
        }
    }
}

/// <summary>
/// Class which caches all the reflection information which are dictionaries.
/// Their keys are always the property name, values are like the getter / setter
/// delegates, types, or validation rules.
/// If not specified, all are initialized within .ctor of
/// <see cref="ValueGetter{T}"/> or <see cref="ValueSetter{T}"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReflectionMetaInfo<T>
{
    /// <summary>
    /// Ordering of properties. [Initialized by <see cref="ReflectionUtils"/>]
    /// </summary>
    public Dictionary<string, int> Ordering { get; } = new();
    public Dictionary<string, Func<T, object>> Getters { get; } = new();
    public Dictionary<string, Action<T, object?>> Setters { get; } = new();
    public Dictionary<string, Type> PropertyTypes { get; } = new();
}

public class ValueGetter<T> : PropertyReflectionHelper<T>
{
    public ValueGetter(IEnumerable<PropertyInfo> properties, ReflectionMetaInfo<T> rmi, AttributeMetaInfo ami) : base(rmi, ami)
    {
        foreach (var property in properties)
        {
            ReflectionInfo.Getters[property.Name] = ReflectionUtils.BuildUntypedGetter<T>(property);
            ReflectionInfo.PropertyTypes[property.Name] = property.PropertyType;
        }
    }

    /// <summary>
    /// Gets the value in property (with name <paramref name="propertyName"/>) from object
    /// <paramref name="targetObject"/>.
    /// It is way faster than ordinary reflection method.
    /// </summary>
    /// <param name="targetObject"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public object? Get(T targetObject, string propertyName)
    {
        return ReflectionInfo.Getters[propertyName].Invoke(targetObject);
    }

    /// <summary>
    /// Gets the value + type tuple of property (with name <paramref name="propertyName"/>) from object
    /// <paramref name="targetObject"/>.
    /// </summary>
    /// <param name="targetObject"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public (object?, Type) GetTypeAndValue(T targetObject, string propertyName)
    {
        return (ReflectionInfo.Getters[propertyName].Invoke(targetObject), ReflectionInfo.PropertyTypes[propertyName]);
    }

    /// <summary>
    /// Go through all validation rules specified in <see cref="ValidationAttribute"/>s,
    /// validate the properties, and throws if any rule being violated.
    /// </summary>
    /// <param name="entry"></param>
    /// <exception cref="ArgumentException"></exception>
    public void ValidateOrThrow(T entry)
    {
        if (!Validate(entry, out var propName, out var ruleName))
            throw new ArgumentException($"Failed validation for property {propName} in type {typeof(T).Name} against rule {ruleName}");
    }

    /// <summary>
    /// Go through all validation rules specified in <see cref="ValidationAttribute"/>s
    /// for the given <paramref name="entry"/>.
    /// Validate its properties, and if any invalid case exists, returns false, outputs the first invalid
    /// property name and the name of validation attribute. Otherwise returns true.
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="invalidPropertyName"></param>
    /// <param name="violatedRule"></param>
    /// <returns></returns>
    public bool Validate(T entry, out string invalidPropertyName, out string violatedRule)
    {
        violatedRule = "";
        invalidPropertyName = "";
        foreach (var (propertyName, attributes) in AttributeInfo.Validations)
        {
            var value = Get(entry, propertyName);
            foreach (var attr in attributes)
            {
                if (!attr.IsValid(value))
                {
                    invalidPropertyName = propertyName;
                    violatedRule = attr.GetType().Name.Replace("Attribute", "");
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Validate a property specified by <paramref name="propertyName"/>
    /// for a given object <paramref name="entry"/>.
    /// If it is invalid, returns false, and outputs an error message.
    /// Otherwise returns true.
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="propertyName"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool Validate(T entry, string propertyName, out string message)
    {
        message = "";
        var value = Get(entry, propertyName);
        if (AttributeInfo.Validations.TryGetValue(propertyName, out var validationAttributes))
        {
            foreach (var attr in validationAttributes)
            {
                if (!attr.IsValid(value))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Check a property specified by <paramref name="propertyName"/>
    /// for a given object <paramref name="entry"/>.
    /// If it is attached by <see cref="AutoCorrectAttribute"/>,
    /// returns true, outputs the related property value and the attribute instance.
    /// Otherwise returns false.
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="propertyName"></param>
    /// <param name="originalValue"></param>
    /// <param name="attribute"></param>
    /// <returns></returns>
    public bool IsWithAutoCorrect(T entry, string propertyName, out object? originalValue, [NotNullWhen(true)] out AutoCorrectAttribute? attribute)
    {
        originalValue = null;
        if (AttributeInfo.AutoCorrections.TryGetValue(propertyName, out attribute))
        {
            originalValue = Get(entry, propertyName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get the property names and values of a given <paramref name="entry"/>.
    /// </summary>
    /// <param name="entry"></param>
    /// <returns></returns>
    public IEnumerable<(string, object)> GetNamesAndValues(T entry)
    {
        foreach (var (name, getter) in ReflectionInfo.Getters)
        {
            yield return (name, getter(entry));
        }
    }
}

public class ValueSetter<T> : PropertyReflectionHelper<T>
{
    public ValueSetter(IEnumerable<PropertyInfo> properties, ReflectionMetaInfo<T> rmi, AttributeMetaInfo ami) : base(rmi, ami)
    {
        foreach (var property in properties)
        {
            var setter = ReflectionUtils.BuildUntypedSetter<T>(property);
            if (setter != null)
                ReflectionInfo.Setters[property.Name] = setter;
            ReflectionInfo.PropertyTypes[property.Name] = property.PropertyType;
        }
    }

    /// <summary>
    /// Sets the value for property (with name <paramref name="propertyName"/>) in object
    /// <paramref name="targetObject"/>.
    /// It is way faster than ordinary reflection method.
    /// </summary>
    /// <param name="targetObject"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public void Set(T targetObject, string propertyName, object? value)
    {
        ReflectionInfo.Setters[propertyName].Invoke(targetObject, value);
    }
}

public abstract class PropertyReflectionHelper<T>
{
    public ReflectionMetaInfo<T> ReflectionInfo { get; private set; }
    public AttributeMetaInfo AttributeInfo { get; private set; }

    protected PropertyReflectionHelper(ReflectionMetaInfo<T> rmi, AttributeMetaInfo ami)
    {
        ReflectionInfo = rmi;
        AttributeInfo = ami;
    }

    public IEnumerable<string> GetNames()
    {
        return ReflectionInfo.Ordering.Keys;
    }

    public int GetIndex(string propertyName)
    {
        return ReflectionInfo.Ordering.TryGetValue(propertyName, out var index) ? index : -1;
    }
}