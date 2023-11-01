using TradeCommon.Runtime;

namespace Common;
public static class TypeConverter
{
    public static TypeCode ToTypeCode(string typeString)
    {
        return typeString switch
        {
            "STRING" => TypeCode.String,
            "INT" or "INT32" or "INTEGER" => TypeCode.Int32,
            "DOUBLE" => TypeCode.Double,
            "DECIMAL" => TypeCode.Decimal,
            "DATE" or "TIME" or "DATETIME" or "TIMESTAMP" => TypeCode.DateTime,
            "BOOL" or "BOOLEAN" => TypeCode.Boolean,
            "LONG" or "INT64" => TypeCode.Int64,
            _ => throw new ArgumentNullException($"Type value {typeString} is invalid."),
        };
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
        return type == typeof(TimeSpan?)
            ? TypeCode.DateTime
            : type == typeof(bool?)
            ? TypeCode.Boolean
            : type == typeof(long?) ? TypeCode.Int64 : type.IsEnum ? TypeCode.Object : TypeCode.Object;
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
        return type == typeof(string)
            || type == typeof(char)
            || type == typeof(char?)
            ? "VARCHAR"
            : type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?)
            ? "DATE"
            : type == typeof(bool) || type == typeof(bool?) ? "BOOLEAN" : type.IsEnum ? "VARCHAR" : "VARCHAR";
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
        return type == typeof(string)
            || type == typeof(char)
            || type == typeof(char?)
            ? "VARCHAR"
            : type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?)
            ? "TIMESTAMP"
            : type == typeof(bool) || type == typeof(bool?) ? "BOOLEAN" : type.IsEnum ? "VARCHAR" : "VARCHAR";
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
        return type == typeof(string) && containsUnicode
            ? "NVARCHAR"
            : type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(DateTime?)
            || type == typeof(TimeSpan?)
            ? "DATETIMEOFFSET"
            : type == typeof(bool) || type == typeof(bool?) ? "BIT" : type.IsEnum ? "VARCHAR" : "VARCHAR";
    }

    public static Type FromTypeCode(TypeCode typeCode)
    {
        switch (typeCode)
        {
            case TypeCode.Object:
            case TypeCode.Empty:
            case TypeCode.DBNull:
                return typeof(object);
            case TypeCode.Boolean:
                return typeof(bool);
            case TypeCode.Char:
                return typeof(char);
            case TypeCode.SByte:
            case TypeCode.Byte:
                return typeof(byte);
            case TypeCode.Int16:
                return typeof(short);
            case TypeCode.UInt16:
                return typeof(ushort);
            case TypeCode.Int32:
                return typeof(int);
            case TypeCode.UInt32:
                return typeof(uint);
            case TypeCode.Int64:
                return typeof(long);
            case TypeCode.UInt64:
                return typeof(ulong);
            case TypeCode.Single:
                return typeof(float);
            case TypeCode.Double:
                return typeof(double);
            case TypeCode.Decimal:
                return typeof(decimal);
            case TypeCode.DateTime:
                return typeof(DateTime);
            case TypeCode.String:
                return typeof(string);
        };
        throw Exceptions.Impossible();
    }
}
