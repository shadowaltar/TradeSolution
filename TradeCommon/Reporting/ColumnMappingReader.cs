using Common;
using CsvHelper;
using log4net;
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
                var typeString = csvReader["Type"] ?? throw new ArgumentNullException("Type column / value missing in definition.");
                typeString = typeString.ToUpperInvariant();

                var cd = new ColumnDefinition
                {
                    Index = csvReader["Index"].ParseInt(-1),
                    FieldName = csvReader["Field"] ?? throw new ArgumentNullException("Field column / value missing in definition."),
                    Caption = csvReader["Caption"] ?? throw new ArgumentNullException("Caption column / value missing in definition."),
                    Type = TypeConverter.ToTypeCode(typeString),
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
            var t = TypeConverter.ToTypeCode(property.PropertyType);
            if (t == TypeCode.Object && !property.PropertyType.IsEnum)
            {
                var prefix = name[0] + ".";
                var innerColumns = Read(property.PropertyType);
                for (int i = 0; i < innerColumns.Count; i++)
                {
                    ColumnDefinition? innerColumn = innerColumns[i];
                    if (!innerColumn.FieldName.StartsWith(prefix))
                    {
                        innerColumn.FieldName = $"{prefix}{innerColumn.FieldName}";
                    }
                    innerColumn.Caption = innerColumn.FieldName;
                    innerColumn.Index = count;
                    definitions.Add(innerColumn);
                    count++;
                }
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
        return t switch
        {
            TypeCode.Decimal or TypeCode.Double => "General",
            TypeCode.Int64 or TypeCode.Int32 => "0",
            TypeCode.String or TypeCode.Boolean => "@",
            TypeCode.DateTime => "yyyy-MM-dd HH:mm:ss",
            _ => "@",
        };
    }
}