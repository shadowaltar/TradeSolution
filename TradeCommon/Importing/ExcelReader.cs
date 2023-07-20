using Common;
using log4net;
using OfficeOpenXml;
using TradeCommon.Importing;
using TradeCommon.Reporting;

namespace TradeDataCore.Importing;
public class ExcelReader
{
    private static readonly ILog _log = Logger.New();

    public List<T>? ReadSheet<T>(string filePath, string columnDefinitionResourcePath, ExcelImportSetting? setting = null)
        where T : class, new()
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath)) return null;

        var columns = ColumnMappingReader.Read(columnDefinitionResourcePath);
        if (columns == null) return null;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var package = new ExcelPackage(stream);

        var sheetName = setting?.SheetName;
        var sheet = sheetName.IsBlank() ? package.Workbook.Worksheets.First() : package.Workbook.Worksheets[sheetName];
        var start = sheet.Dimension.Start;
        var end = sheet.Dimension.End;
        var results = new List<T>();

        var headerSkipLineCount = setting?.HeaderSkipLineCount ?? 0;

        var headers = new Dictionary<int, ColumnDefinition>();
        for (int j = start.Column; j <= end.Column; j++)
        {
            var i = start.Row + headerSkipLineCount;
            var cellValue = sheet.Cells[i, j].Text;
            var column = columns.FirstOrDefault(c => c.Caption == cellValue);
            if (column == null)
                continue;

            headers[j] = column;
        }

        var vs = ReflectionUtils.GetValueSetter<T>();

        var hardcodedColumns = GetHardcodedValueAndColumns(setting?.HardcodedValues, columns);
        for (int i = start.Row + 1 + headerSkipLineCount; i <= end.Row; i++) // line 0 is header
        {
            var entry = new T();

            for (int j = start.Column; j <= end.Column; j++)
            {
                var cellValue = sheet.Cells[i, j].Text;
                if (headers.TryGetValue(j, out var column))
                {
                    var actualFieldName = ProcessDeeper(column.FieldName, out var inner);
                    if (inner.innerObject != null && inner.firstLevelFieldName != null)
                    {
                        // we don't need to create 1st level object when it is a nullable 2nd level field
                        if (cellValue.IsBlank() && column.IsNullable)
                            continue;

                        vs.Set(entry, inner.firstLevelFieldName, inner.innerObject);

                        var firstLevelType = inner.innerObject.GetType();
                        var value = ParseCell(cellValue, column);
                        if (value is string strObj && !column.IsNullable && strObj.IsBlank())
                        {
                            _log.Warn($"Skipping this value since the column {column.FieldName} has a null/empty string value. Row id: {i}");
                            entry = null;
                            break;
                        }
                        inner.innerObject.SetPropertyValue(firstLevelType, actualFieldName, value);
                    }
                    else
                    {
                        var value = ParseCell(cellValue, column);
                        if (value is string strObj && !column.IsNullable && strObj.IsBlank())
                        {
                            _log.Warn($"Skipping this value since the column {column.FieldName} has a null/empty string value. Row id: {i}");
                            entry = null;
                            break;
                        }
                        vs.Set(entry, actualFieldName, value);
                    }
                }
            }
            // some values are directly hardcoded
            if (entry != null && hardcodedColumns != null)
            {
                foreach (var (col, hardcodedValue) in hardcodedColumns)
                {
                    if (hardcodedValue is ComplexMapping cm)
                    {
                        try
                        {
                            var f = cm.Function;
                            var paramName = cm.ParameterFieldName;
                            var paramValue = entry.GetPropertyValue(typeof(T), paramName);
                            vs.Set(entry, col.FieldName, f(paramValue));
                        }
                        catch (Exception e)
                        {
                            _log.Error("Failed to map a computed value.", e);
                        }
                    }
                    else
                    {
                        vs.Set(entry, col.FieldName, hardcodedValue);
                    }
                }
            }

            if (entry != null)
            {
                results.Add(entry);
            }
        }
        return results;
    }

    private static List<(ColumnDefinition c, object v)>? GetHardcodedValueAndColumns(Dictionary<string, object>? hardcodedValues, List<ColumnDefinition> columns)
    {
        if (hardcodedValues == null) return null;

        var results = new List<(ColumnDefinition c, object v)>();
        foreach (var (colName, val) in hardcodedValues)
        {
            var column = columns.FirstOrDefault(c => c.FieldName == colName);
            if (column == null) continue;

            results.Add((column, val));
        }
        return results;
    }

    private object? ParseCell(string? cellValue, ColumnDefinition column)
    {
        if (cellValue == null) return null;
        switch (column.Type)
        {
            case TypeCode.Char:
            case TypeCode.String:
                return cellValue;
            case TypeCode.Decimal:
                return cellValue.ParseDecimal();
            case TypeCode.DateTime:
                return cellValue.ParseDate(column.Format);
            case TypeCode.Int32:
                return cellValue.ParseInt();
            case TypeCode.Boolean:
                return cellValue.ParseBool();
            case TypeCode.Double:
                if (column.Format?.Contains("%") ?? false)
                    return cellValue.ParsePercentage();
                return cellValue.ParseDouble();
            case TypeCode.Int64:
                return cellValue.ParseLong();
            default:
                return null;
        }
    }

    private string ProcessDeeper(string fieldName, out (string? firstLevelFieldName, object? innerObject) inner)
    {
        inner = (null, null);
        if (fieldName.Contains('.'))
        {
            var fields = fieldName.Split('.');
            if (fields.Length == 1)
                return fieldName;

            if (fields.Length != 2)
                throw new ArgumentException("Only support ABC or ABC.DEF two kinds of fieldName.");

            // only support 2 levels
            var type = ReflectionUtils.SearchType(fields[0]);
            if (type != null)
            {
                inner = (fields[0], Activator.CreateInstance(type));
                return fields[1];
            }
        }
        return fieldName;
    }
}

