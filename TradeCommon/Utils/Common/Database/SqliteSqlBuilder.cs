using Common.Attributes;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Database;

namespace Common.Database;

public class SqliteSqlBuilder : IDatabaseSqlBuilder
{
    public string CreateInsertSql<T>(char placeholderPrefix, string? tableNameOverride = null) where T : class
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null) throw new InvalidOperationException("Must provide table name.");

        var tableName = tableNameOverride ?? attr.TableName;
        var properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        var uniqueKeyTuples = typeof(T).GetDistinctAttributes<UniqueAttribute>();
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
        sb.AppendLine("VALUES").Append('(');
        foreach (var name in targetFieldNames)
        {
            if (ignoreFieldNames.Contains(name))
                continue;
            sb.Append(targetFieldNamePlaceHolders[name]).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')');
        return sb.ToString();
    }

    // TODO
    public string CreateUpsertSql<T>(char placeholderPrefix, string? tableNameOverride = null) where T : class
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null) throw new InvalidOperationException("Must provide table name.");

        var tableName = tableNameOverride ?? attr.TableName;
        var properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        var uniqueKeyTuples = typeof(T).GetDistinctAttributes<UniqueAttribute>();
        var targetFieldNames = properties.Select(pair => pair.Key).ToList();
        var ignoreFieldNames = ReflectionUtils.GetAttributeInfo<T>().DatabaseIgnoredPropertyNames;

        // DELETE the uniques
        var sb = new StringBuilder()
            .Append("DELETE FROM ").AppendLine(tableName).Append('\t').Append("WHERE ");

        for (int i = 0; i < uniqueKeyTuples.Count; i++)
        {
            UniqueAttribute? uniqueAttr = uniqueKeyTuples[i];
            var names = uniqueAttr.FieldNames;
            sb.Append('(');
            for (int j = 0; j < names.Length; j++)
            {
                string? name = names[j];
                sb.Append(name).Append(" = ").Append(placeholderPrefix).Append(name);
                if (j != names.Length - 1)
                {
                    sb.Append(" AND ");
                }
            }
            sb.Append(')');
            if (i != uniqueKeyTuples.Count - 1)
            {
                sb.Append(" OR ");
            }
        }
        sb.Append(';').AppendLine();

        var insertSql = CreateInsertSql<T>(placeholderPrefix, tableName);
        sb.Append(insertSql).Append(';');
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
        var storageAttribute = type.GetCustomAttribute<StorageAttribute>();
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

        // sort the properties
        List<KeyValuePair<string, PropertyInfo>> propertyList;
        if (storageAttribute == null || storageAttribute.SortProperties)
        {
            var sortedProperties = properties.OrderBy(p => p.Key).ToList();
            var idProperty = sortedProperties.FirstOrDefault(p => p.Key == "Id");
            if (idProperty.Value != null)
            {
                sortedProperties.Move(idProperty, 0);
            }
            propertyList = sortedProperties;
        }
        else
        {
            propertyList = properties.ToList();
        }

        var specialColumnOrdering = new Dictionary<string, int>();
        var ignoredColumns = new HashSet<string>();
        var typeStrings = new Dictionary<string, string>();
        var requiredColumns = new HashSet<string>();
        var autoIncreaseColumns = new HashSet<string>();
        var defaultColumnValues = new Dictionary<string, object?>();
        var varcharColumnLengths = new Dictionary<string, int>();
        foreach (var (name, property) in propertyList)
        {
            typeStrings[name] = TypeConverter.ToSqliteType(property.PropertyType);
            var propAttributes = property.GetCustomAttributes().ToList();
            if (recordPropertyAttributes.TryGetValue(name, out var otherAttributes))
            {
                propAttributes.AddRange(otherAttributes);
            }
            foreach (var attr in propAttributes)
            {
                if (attr is DatabaseIgnoreAttribute && attr is not AutoIncrementOnInsertAttribute)
                {
                    ignoredColumns.Add(name);
                    break;
                }

                if (attr is RequiredMemberAttribute) // system attribute
                {
                    requiredColumns.Add(name); // TODO not used
                }
                if (attr is ColumnAttribute colAttr)
                {
                    if (colAttr.Order != -1)
                    {
                        specialColumnOrdering[name] = colAttr.Order;
                    }
                }

                if (attr is not IStorageRelatedAttribute)
                    continue;

                if (attr is AutoIncrementOnInsertAttribute)
                {
                    autoIncreaseColumns.Add(name);
                }
                if (attr is DefaultValueAttribute defaultAttr)
                {
                    defaultColumnValues[name] = defaultAttr.Value;
                }
                if (attr is LengthAttribute lengthAttr && lengthAttr.MaxLength > 0)
                {
                    varcharColumnLengths[name] = lengthAttr.MaxLength;
                }
                if (attr is AsJsonAttribute)
                {
                    typeStrings[name] = "TEXT";
                }
            }
        }
        propertyList = propertyList.Where(p => !ignoredColumns.Contains(p.Key)).ToList();

        // handle special ordering of columns
        foreach (var (scName, index) in specialColumnOrdering)
        {
            var p = propertyList.FirstOrDefault(p => p.Key == scName);
            propertyList.Move(p, index);
        }

        // construct sql
        foreach (var (name, property) in propertyList)
        {
            var varcharMax = varcharColumnLengths.GetOrDefault(name);
            var defaultValue = defaultColumnValues.GetOrDefault(name);
            var isNotNull = false;
            var propertyGet = property.GetGetMethod();
            if (propertyGet != null)
            {
                var returnParameter = propertyGet.ReturnParameter;
                isNotNull = Attribute.IsDefined(returnParameter, typeof(NotNullAttribute));
            }

            sb.Append(name).Append(' ').Append(typeStrings[name]);
            if (property.PropertyType == typeof(string) && varcharMax > 0)
                sb.Append('(').Append(varcharMax).Append(')');
            else if (property.PropertyType.IsEnum)
                sb.Append('(').Append(Consts.EnumDatabaseTypeSize).Append(')');

            if (autoIncreaseColumns.Contains(name))
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
            sb.Append($"CREATE UNIQUE INDEX IF NOT EXISTS ")
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
        return uniqueKeys.IsNullOrEmpty()
            ? ""
            : @$"
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
        var uniqueKeyTuples = typeof(T).GetDistinctAttributes<UniqueAttribute>();
        var sb = new StringBuilder("DELETE FROM ")
            .Append(tableName)
            .Append(" WHERE ");
        //for (int i = 0; i < uniqueKey.Length; i++)
        //{
        //    string? name = uniqueKey[i];
        //    sb.Append(name).Append(" = ").Append(placeholderPrefix).Append(name);
        //    if (i != uniqueKey.Length - 1)
        //        sb.Append(" AND ");
        //}
        for (int i = 0; i < uniqueKeyTuples.Count; i++)
        {
            UniqueAttribute? uniqueAttr = uniqueKeyTuples[i];
            var names = uniqueAttr.FieldNames;
            sb.Append('(');
            for (int j = 0; j < names.Length; j++)
            {
                string? name = names[j];
                sb.Append(name).Append(" = ").Append(placeholderPrefix).Append(name);
                if (j != names.Length - 1)
                {
                    sb.Append(" AND ");
                }
            }
            sb.Append(')');
            if (i != uniqueKeyTuples.Count - 1)
            {
                sb.Append(" OR ");
            }
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
