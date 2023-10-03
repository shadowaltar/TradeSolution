using Common.Attributes;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Database;

namespace Common.Database;

public class SqliteSchemaHelper : IDatabaseSchemaHelper
{
    public string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert, string? tableNameOverride = null) where T : class
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null) throw new InvalidOperationException("Must provide table name.");

        var tableName = tableNameOverride ?? attr.TableName;
        var properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        var uniqueKeyNames = typeof(T).GetDistinctAttributes<UniqueAttribute>()
            .FirstOrDefault()?.FieldNames ?? Array.Empty<string>();
        var targetFieldNames = properties.Select(pair => pair.Key).ToList();
        var targetFieldNamePlaceHolders = targetFieldNames.ToDictionary(fn => fn, fn => placeholderPrefix + fn);

        var ignoreFieldNames = ReflectionUtils.GetAttributeInfo<T>().DatabaseIgnoredPropertyNames;

        // INSERT INTO (...)
        var sb = new StringBuilder()
            .Append("INSERT INTO ").AppendLine(tableName).Append('(');
        foreach (var name in targetFieldNames)
        {
            if (ignoreFieldNames.Contains(name))
                continue;
            sb.Append(name).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        // VALUES (...)
        sb.AppendLine("VALUES").AppendLine().Append('(');
        foreach (var name in targetFieldNames)
        {
            if (ignoreFieldNames.Contains(name))
                continue;
            sb.Append(targetFieldNamePlaceHolders[name]).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        if (isUpsert && !uniqueKeyNames.IsNullOrEmpty())
        {
            // ON CONFLICT (...)
            sb.Append("ON CONFLICT (");
            foreach (var fn in uniqueKeyNames)
            {
                sb.Append(fn).Append(',');
            }
            sb.RemoveLast();
            sb.Append(')').AppendLine();

            // DO UPDATE SET ...
            sb.Append("DO UPDATE SET ");
            foreach (var fn in targetFieldNames)
            {
                if (ignoreFieldNames.Contains(fn))
                    continue;
                if (uniqueKeyNames.Contains(fn))
                    continue;

                sb.Append(fn).Append(" = excluded.").Append(fn).Append(',');
            }
            sb.RemoveLast();
        }
        return sb.ToString();
    }

    public string CreateDropTableAndIndexSql<T>(string? tableNameOverride = null) where T : class
    {
        var type = typeof(T);
        var (table, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        table = tableNameOverride ?? table;

        var sb = new StringBuilder();
        sb.Append($"DROP TABLE IF EXISTS ").Append(table).AppendLine(";");

        var uniqueAttributes = type.GetDistinctAttributes<UniqueAttribute>().Distinct().ToList();
        var indexAttributes = type.GetDistinctAttributes<IndexAttribute>().Distinct().ToList();

        for (int i = 0; i < uniqueAttributes.Count; i++)
        {
            var attr = uniqueAttributes[i];
            sb.Append($"DROP INDEX IF EXISTS ")
                .Append("UX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine(";");
        }
        for (int i = 0; i < indexAttributes.Count; i++)
        {
            var attr = indexAttributes[i];
            sb.Append($"DROP INDEX IF EXISTS ")
                .Append("IX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine(";");
        }
        return sb.ToString();
    }

    public string CreateCreateTableAndIndexSql<T>(string? tableNameOverride = null) where T : class
    {
        var type = typeof(T);
        var (table, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        table = tableNameOverride ?? table;

        var properties = ReflectionUtils.GetPropertyToName(type);
        var uniqueAttributes = type.GetDistinctAttributes<UniqueAttribute>().ToList();
        var indexAttributes = type.GetDistinctAttributes<IndexAttribute>().ToList();
        var primaryUniqueKeys = uniqueAttributes.FirstOrDefault()?.FieldNames;

        // find attributes attached to a record type's constructor parameter
        var recordPropertyAttributes = new Dictionary<string, List<Attribute>>();
        if (type.IsRecord())
        {
            var constructors = type.GetConstructors();
            foreach (var constructor in constructors)
            {
                var ctorParams = constructor.GetParameters();
                foreach (var ctorParam in ctorParams)
                {
                    if (properties.ContainsKey(ctorParam.Name!))
                    {
                        // it should be a property generated by the record constructor
                        recordPropertyAttributes[ctorParam.Name!] = ctorParam.GetCustomAttributes().ToList();
                    }
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE IF NOT EXISTS ")
            .Append(table).AppendLine(" (");

        var sortedProperties = properties.OrderBy(p => p.Key).ToList();
        var idProperty = sortedProperties.FirstOrDefault(p => p.Key == "Id");
        if (idProperty.Value != null)
        {
            sortedProperties.Remove(idProperty);
            sortedProperties.Insert(0, idProperty);
        }
        foreach (var (name, property) in sortedProperties)
        {
            var typeString = TypeConverter.ToSqliteType(property.PropertyType);

            var isIgnored = false;
            var isNotNull = false;
            var isPrimary = false;
            object? defaultValue = null;
            int varcharMax = 0;
            var attributes = property.GetCustomAttributes().ToList();
            if (recordPropertyAttributes.TryGetValue(name, out var otherAttributes))
            {
                attributes.AddRange(otherAttributes);
            }
            foreach (var attr in attributes)
            {
                if (attr is RequiredMemberAttribute) // system attribute
                {
                    isNotNull = true;
                }

                if (attr is not IStorageRelatedAttribute)
                    continue;

                if (attr is DatabaseIgnoreAttribute)
                {
                    isIgnored = true;
                    break;
                }

                if (attr is AutoIncrementOnInsertAttribute)
                {
                    isPrimary = true;
                }
                if (attr is DefaultValueAttribute defaultAttr)
                {
                    defaultValue = defaultAttr.Value;
                }
                if (attr is LengthAttribute lengthAttr && lengthAttr.MaxLength > 0)
                {
                    varcharMax = lengthAttr.MaxLength;
                }
                if (attr is AsJsonAttribute)
                {
                    typeString = "TEXT";
                }
            }

            if (isIgnored) continue;

            var propertyGet = property.GetGetMethod();
            if (propertyGet != null)
            {
                var returnParameter = propertyGet.ReturnParameter;
                isNotNull = Attribute.IsDefined(returnParameter, typeof(NotNullAttribute));
            }

            sb.Append(name).Append(' ').Append(typeString);
            if (property.PropertyType == typeof(string) && varcharMax > 0)
                sb.Append('(').Append(varcharMax).Append(')');
            else if (property.PropertyType.IsEnum)
                sb.Append('(').Append(Consts.EnumDatabaseTypeSize).Append(')');

            if (isPrimary)
                sb.Append(" PRIMARY KEY");
            else if (isNotNull)
                sb.Append(" NOT NULL");
            if (defaultValue != null)
            {
                if (property.PropertyType == typeof(string))
                    sb.Append(" DEFAULT '").Append(defaultValue).Append('\'');
                else
                    sb.Append(" DEFAULT ").Append(defaultValue);
            }
            sb.Append(',');
        }
        if (primaryUniqueKeys.IsNullOrEmpty())
            sb.RemoveLast();
        else
            sb.AppendLine().Append("UNIQUE(").AppendJoin(',', primaryUniqueKeys).AppendLine(")");
        sb.AppendLine(");");

        foreach (var attr in uniqueAttributes)
        {
            sb.Append($"CREATE UNIQUE INDEX ")
                .Append("UX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine()
                .Append("ON ").AppendLine(table)
                .Append('(').AppendJoin(',', attr.FieldNames).AppendLine(");");
        }
        foreach (var attr in indexAttributes)
        {
            sb.Append($"CREATE INDEX ")
                .Append("IX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine()
                .Append("ON ").AppendLine(table)
                .Append('(').AppendJoin(',', attr.FieldNames).AppendLine(");");
        }
        return sb.ToString();
    }

    public string GetCreateTableUniqueClause<T>()
    {
        var type = typeof(T);
        var uniqueKeys = type.GetCustomAttribute<UniqueAttribute>()?.FieldNames;
        return uniqueKeys.IsNullOrEmpty() ? "" : $", UNIQUE({string.Join(", ", uniqueKeys)})";
    }

    public string GetCreateTableUniqueIndexStatement<T>(string tableName)
    {
        var type = typeof(T);
        var uniqueKeys = type.GetCustomAttribute<UniqueAttribute>()?.FieldNames;
        if (uniqueKeys.IsNullOrEmpty())
            return "";
        return @$"
CREATE UNIQUE INDEX
    idx_{tableName}_{string.Join("_", uniqueKeys.Select(k => k.FirstCharLowerCase()))}
ON {tableName}
    ({string.Join(", ", uniqueKeys)});";
    }

    public string GetDropTableUniqueIndexStatement<T>(string tableName)
    {
        var type = typeof(T);
        var attributeInfo = ReflectionUtils.GetAttributeInfo<T>();
        var sb = new StringBuilder();
        foreach (var keys in attributeInfo.AllUniqueKeys)
        {
            if (keys.IsNullOrEmpty())
                continue;
            sb.Append("DROP INDEX IF EXISTS IX_").Append(tableName).AppendJoin('_', keys).AppendLine(";");
        }
        return sb.ToString();
    }

    public string CreateDeleteSql<T>(char placeholderPrefix = Consts.SqlCommandPlaceholderPrefix,
                                     string? tableNameOverride = null) where T : class
    {
        var (tableName, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        tableName = tableNameOverride ?? tableName;
        var attributeInfo = ReflectionUtils.GetAttributeInfo<T>();
        var uniqueKey = attributeInfo.PrimaryUniqueKey.ToArray();
        var sb = new StringBuilder("DELETE FROM ")
            .Append(tableName)
            .Append(" WHERE ");
        for (int i = 0; i < uniqueKey.Length; i++)
        {
            string? name = uniqueKey[i];
            sb.Append(name).Append(" = ").Append(placeholderPrefix).Append(name);
            if (i != uniqueKey.Length - 1)
                sb.Append(" AND ");
        }
        return sb.ToString();
    }

    public string CreateDeleteSql<T>(string whereClause,
                                     string? tableNameOverride = null) where T : class
    {
        var (tableName, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        tableName = tableNameOverride ?? tableName;
        var attributeInfo = ReflectionUtils.GetAttributeInfo<T>();
        var uniqueKey = attributeInfo.PrimaryUniqueKey.ToArray();
        var sb = new StringBuilder("DELETE FROM ")
            .Append(tableName)
            .Append(" WHERE ")
            .Append(whereClause);
        return sb.ToString();
    }
}
