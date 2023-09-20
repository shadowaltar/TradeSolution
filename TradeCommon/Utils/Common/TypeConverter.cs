namespace Common;
public static class TypeConverter
{
    public static TypeCode ToTypeCode(string typeString)
    {
        switch (typeString)
        {
            case "STRING":
                return TypeCode.String;
            case "INT":
            case "INT32":
            case "INTEGER":
                return TypeCode.Int32;
            case "DOUBLE":
                return TypeCode.Double;
            case "DECIMAL":
                return TypeCode.Decimal;
            case "DATE":
            case "TIME":
            case "DATETIME":
            case "TIMESTAMP":
                return TypeCode.DateTime;
            case "BOOL":
            case "BOOLEAN":
                return TypeCode.Boolean;
            case "LONG":
            case "INT64":
                return TypeCode.Int64;
            default:
                throw new ArgumentNullException($"Type value {typeString} is invalid.");
        }
    }

    public static TypeCode ToTypeCode(Type type)
    {
        if (type == typeof(int))
            return TypeCode.Int32;
        if (type == typeof(double))
            return TypeCode.Double;
        if (type == typeof(decimal))
            return TypeCode.Decimal;
        if (type == typeof(DateTime))
            return TypeCode.DateTime;
        if (type == typeof(TimeSpan))
            return TypeCode.DateTime;
        if (type == typeof(bool))
            return TypeCode.Boolean;
        if (type == typeof(long))
            return TypeCode.Int64;
        if (type == typeof(string))
            return TypeCode.String;
        if (type == typeof(int?))
            return TypeCode.Int32;
        if (type == typeof(double?))
            return TypeCode.Double;
        if (type == typeof(decimal?))
            return TypeCode.Decimal;
        if (type == typeof(DateTime?))
            return TypeCode.DateTime;
        if (type == typeof(TimeSpan?))
            return TypeCode.DateTime;
        if (type == typeof(bool?))
            return TypeCode.Boolean;
        if (type == typeof(long?))
            return TypeCode.Int64;
        if (type.IsEnum)
            return TypeCode.Object;

        return TypeCode.Object;
    }

    public static string ToSqliteType(Type type)
    {
        if (type == typeof(int)
            || type == typeof(long)
            || type == typeof(int?)
            || type == typeof(long?))
            return "INTEGER";

        if (type == typeof(decimal)
            || type == typeof(double)
            || type == typeof(decimal?)
            || type == typeof(double?))
            return "REAL";
        if (type == typeof(string)
            || type == typeof(char)
            || type == typeof(char?))
            return "VARCHAR";

        if (type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?))
            return "DATE";

        if (type == typeof(bool) || type == typeof(bool?))
            return "BOOLEAN";

        if (type.IsEnum)
            return "VARCHAR";

        return "VARCHAR";
    }

    public static string ToSnowflakeType(Type type)
    {
        if (type == typeof(int)
            || type == typeof(long)
            || type == typeof(int?)
            || type == typeof(long?))
            return "INTEGER";

        if (type == typeof(decimal)
            || type == typeof(double)
            || type == typeof(decimal?)
            || type == typeof(double?))
            return "REAL";
        if (type == typeof(string)
            || type == typeof(char)
            || type == typeof(char?))
            return "VARCHAR";

        if (type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?))
            return "TIMESTAMP";

        if (type == typeof(bool) || type == typeof(bool?))
            return "BOOLEAN";

        if (type.IsEnum)
            return "VARCHAR";

        return "VARCHAR";
    }

    public static string ToSqlServerType(Type type, bool containsUnicode = false)
    {
        if (type == typeof(int)
            || type == typeof(int?))
            return "INT";
        if (type == typeof(long)
            || type == typeof(long?))
            return "BIGINT";

        if (type == typeof(decimal)
            || type == typeof(decimal?))
            return "MONEY";
        if (type == typeof(double)
            || type == typeof(double?))
            return "REAL";

        if ((type == typeof(string) && !containsUnicode)
            || type == typeof(char)
            || type == typeof(char?))
            return "VARCHAR";
        if (type == typeof(string) && containsUnicode)
            return "NVARCHAR";

        if (type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?))
            return "DATETIMEOFFSET";

        if (type == typeof(bool) || type == typeof(bool?))
            return "BIT";

        if (type.IsEnum)
            return "VARCHAR";

        return "VARCHAR";
    }
}
