using Common;
using CsvHelper;
using log4net;
using System.Globalization;
using TradeCommon.Utils;

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
                definitions.Add(cd);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to read column mapping file.", ex);
        }
        return definitions;
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
}