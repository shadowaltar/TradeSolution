using Common;
using CsvHelper;
using log4net;
using System.Formats.Asn1;
using System.Globalization;
using TradeCommon.Utils;
using TradeCommon.Utils.Excels;

namespace TradeCommon.Reporting;
public class ColumnMappingReader
{
    private static readonly ILog _log = Logger.New();

    public static List<ColumnDefinition> Read(string fullResourceName)
    {
        var definitions = new List<ColumnDefinition>();
        try
        {
            using var reader = EmbeddedResourceReader.GetStreamReader(fullResourceName);
            using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
            csvReader.Read();
            csvReader.ReadHeader();
            while (csvReader.Read())
            {
                string? sorting = csvReader["Sorting"];
                var sortIndex = !sorting.IsBlank() ? sorting!.Substring(0, sorting.Length - 1).ParseInt() : -1;
                var isAscending = sorting?.EndsWith("a") ?? false;
                var cd = new ColumnDefinition
                {
                    Index = csvReader["Index"].ParseInt(-1),
                    FieldName = csvReader["Field"] ?? throw new ArgumentNullException("Field column / value missing in definition."),
                    Caption = csvReader["Caption"] ?? throw new ArgumentNullException("Caption column / value missing in definition."),
                    Type = ParseType(csvReader),
                    Format = csvReader["Format"],
                    IsNullable = csvReader["Nullable"].ParseBool(),
                    Formula = csvReader["Formula"],
                    SortIndex = sortIndex,
                    IsAscending = isAscending,
                    IsHidden = csvReader["Hidden"].ParseBool(),
                };
                if (cd.IsSpecialObjectColumn)
                {
                    // will find the exact class which represents the Type value
                    var typeName = csvReader["Type"]?.ToString();
                    var type = ReflectionUtils.SearchType(typeName);
                    if (typeName.IsBlank() || type == null)
                    {
                        throw new ArgumentException("Invalid object type specified in the column definition file which no concrete class can be matched to it. Type value: " + typeName);
                    }
                    if (!cd.Format.IsBlank())
                    {
                        var ctor = type.GetConstructor(new[] { typeof(string) });
                        var obj = ctor?.Invoke(new object[] { cd.Format });
                        cd.ConcreteSpecialObject = obj as ISpecialCellObject;
                    }
                    else
                    {
                        var obj = Activator.CreateInstance(type);
                        cd.ConcreteSpecialObject = obj as ISpecialCellObject;
                    }
                }
                definitions.Add(cd);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to read column mapping file.", ex);
        }
        return definitions;
    }

    public static List<ColumnDefinition> Read(Type type)
    {
        var definitions = new List<ColumnDefinition>();

        var properties = ReflectionUtils.GetPropertyToName(type);
        var count = 0;
        foreach (var (name, property) in properties)
        {
            var t = ParseTypeAsDefault(property.PropertyType);
            if (t == TypeCode.Object)
            {
                var innerColumns = Read(property.PropertyType);
                foreach (var innerColumn in innerColumns)
                {
                    innerColumn.FieldName = $"{name}.{innerColumn.FieldName}";
                    innerColumn.Caption = $"{name}.{innerColumn.FieldName}";
                }
                definitions.AddRange(innerColumns);
                count += innerColumns.Count;
            }
            else
            {
                var cd = new ColumnDefinition
                {
                    Index = count,
                    FieldName = name,
                    Caption = name,
                    Type = t,
                    Format = GetDefaultFormat(t),
                    IsNullable = true, // reflection-based definitions are always nullable
                    Formula = "",
                    SortIndex = -1,
                    IsAscending = true,
                    IsHidden = false,
                };
                definitions.Add(cd);
                count++;
            }
        }
        return definitions;
    }

    private static string GetDefaultFormat(TypeCode t)
    {
        switch (t)
        {
            case TypeCode.Decimal:
            case TypeCode.Double:
                return "General";
            case TypeCode.Int64:
            case TypeCode.Int32:
                return "0";
            case TypeCode.String:
            case TypeCode.Boolean:
                return "@";
            case TypeCode.DateTime:
                return "yyyy-MM-dd HH:mm:ss";
        }
        return "@";
    }

    private static TypeCode ParseType(CsvReader csvReader)
    {
        var typeString = csvReader["Type"] ?? throw new ArgumentNullException("Type column / value missing in definition.");
        typeString = typeString.ToUpperInvariant();
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
                throw new ArgumentNullException($"Type value {csvReader["Type"]} is invalid in definition.");
        }
    }

    private static TypeCode ParseTypeAsDefault(Type type)
    {
        if (type == typeof(int))
            return TypeCode.Int32;
        if (type == typeof(double))
            return TypeCode.Double;
        if (type == typeof(decimal))
            return TypeCode.Decimal;
        if (type == typeof(DateTime))
            return TypeCode.DateTime;
        if (type == typeof(bool))
            return TypeCode.Boolean;
        if (type == typeof(long))
            return TypeCode.Boolean;
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
        if (type == typeof(bool?))
            return TypeCode.Boolean;
        if (type == typeof(long?))
            return TypeCode.Boolean;
        return TypeCode.Object;
    }
}