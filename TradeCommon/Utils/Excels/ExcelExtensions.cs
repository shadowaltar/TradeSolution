using OfficeOpenXml;
using OfficeOpenXml.Filter;
using OfficeOpenXml.Style;
using System.Diagnostics;
using System.Drawing;

namespace Common.Excels;
public static class ExcelExtensions
{
    private static readonly ColorConverter _colorConverter = new();

    /// <summary>
    /// Color cache.
    /// </summary>
    private static readonly Dictionary<string, Color> _stringToColors = new();

    public static void SetStyle(this ExcelRange range,
        string fontFamily = "Calibri",
        int size = 11,
        bool isBold = false,
        bool isItalic = false,
        string? background = null,
        string? foreground = null)
    {
        range.Style.Font.Name = fontFamily;
        range.Style.Font.Size = size;
        range.Style.Font.Bold = isBold;
        range.Style.Font.Italic = isItalic;
        if (background != null)
            range.Style.Fill.BackgroundColor.SetBackgroundColor(background, range.Style.Fill);
        if (foreground != null)
            range.Style.Font.Color.SetForegroundColor(foreground);
    }

    public static void SetForegroundColor(this ExcelColor excelColor, string foreground)
    {
        if (foreground.IsBlank())
        {
            excelColor.SetAuto();
            return;
        }
        try
        {
            if (_stringToColors.TryGetValue(foreground, out var color))
            {
                if (color == Color.Transparent)
                    excelColor.SetAuto();
                else
                    excelColor.SetColor(color);
            }
            else
            {
                var nullableColor = _colorConverter.ConvertFromString(foreground);
                if (nullableColor != null)
                {
                    color = (Color)nullableColor;
                    excelColor.SetColor(color);
                }
                else
                {
                    color = Color.Transparent;
                    excelColor.SetAuto();
                }
                _stringToColors[foreground] = color;
            }
        }
        catch
        {
            // string which cannot be parsed
            Debug.WriteLine("Invalid foreground color string which cannot be parsed and set: " + foreground);
            _stringToColors[foreground] = Color.Transparent;
            excelColor.SetAuto();
        }
    }

    public static void SetBackgroundColor(this ExcelColor excelColor, string background, ExcelFill parent)
    {
        if (background.IsBlank())
        {
            ResetColor(excelColor, parent);
            return;
        }
        try
        {
            if (_stringToColors.TryGetValue(background, out var color))
            {
                if (color == Color.Transparent)
                    ResetColor(excelColor, parent);
                else if (parent != null)
                {
                    parent.PatternType = ExcelFillStyle.Solid;
                    excelColor.SetColor(color);
                }
            }
            else
            {
                var nullableColor = _colorConverter.ConvertFromString(background);
                if (nullableColor != null)
                {
                    color = (Color)nullableColor;
                    if (parent != null)
                    {
                        parent.PatternType = ExcelFillStyle.Solid;
                        excelColor.SetColor(color);
                    }
                }
                else
                {
                    color = Color.Transparent;
                    ResetColor(excelColor, parent);
                }
                _stringToColors[background] = color;
            }
        }
        catch
        {
            // string which cannot be parsed
            Debug.WriteLine("Invalid background color string which cannot be parsed and set: " + background);
            ResetColor(excelColor, parent);
        }

        static void ResetColor(ExcelColor ec, ExcelFill? p)
        {
            if (p != null && p.PatternType != ExcelFillStyle.None)
            {
                ec.SetAuto();
            }
        }
    }

    /// <summary>
    /// Filter a column by given its index (0-based) and values to be filtered in.
    /// Notice that if it is against a formula, must specify <paramref name="shouldCalculate"/>=true to trigger formula evaluation.
    /// </summary>
    /// <param name="range"></param>
    /// <param name="shouldCalculate"></param>
    /// <param name="columnIndexAndValues"></param>
    public static void Filter(this ExcelRange range, bool shouldCalculate = false, params (int index, IEnumerable<string> values)[] columnIndexAndValues)
    {
        if (columnIndexAndValues != null)
        {
            var sheet = range.Worksheet;
            range.AutoFilter = true;
            // index is 0-based
            foreach (var (index, values) in columnIndexAndValues)
            {
                var columnFilter = sheet.AutoFilter.Columns.AddCustomFilterColumn(index);
                columnFilter.And = true;
                foreach (var value in values)
                {
                    columnFilter.Filters.Add(new ExcelFilterCustomItem(value, eFilterOperator.Equal));
                }
            }
            if (shouldCalculate)
            {
                sheet.Calculate();
            }
            sheet.AutoFilter.ApplyFilter();
        }
    }

    /// <summary>
    /// Write a list of object to a row starting from the cell of <params cref="cursor"/>,
    /// then return a new cursor one row below the original input cursor.
    /// </summary>
    /// <param name="cursor"></param>
    /// <param name="row"></param>
    /// <returns></returns>
    public static ExcelRangeBase WriteRow(this ExcelRangeBase cursor, params object?[] row)
    {
        ExcelRangeBase excelRangeBase = cursor;
        foreach (object? item in row)
        {
            if (item != null)
            {
                if (item is decimal || item is decimal?)
                {
                    excelRangeBase.Value = Convert.ToDouble(item);
                }
                else if (item is ExcelFormula f)
                {
                    if (f.IsArrayFormula)
                    {
                        excelRangeBase.CreateArrayFormula(f.Formula);
                    }
                    else
                    {
                        if (f.FormulaR1C1 != null)
                            excelRangeBase.FormulaR1C1 = f.FormulaR1C1;
                        else
                            excelRangeBase.Formula = f.FormulaA1;
                    }
                }
                else
                {
                    excelRangeBase.Value = item;
                }
            }
            excelRangeBase = excelRangeBase.Offset(0, 1);
        }
        return cursor.Offset(1, 0);
    }

    /// <summary>
    /// Auto-fit all columns of a sheet, or only those columns which indexes are provided.
    /// </summary>
    /// <param name="sheet"></param>
    /// <param name="columnIndexes"></param>
    public static void AutoFit(this ExcelWorksheet sheet, params int[] columnIndexes)
    {
        if (columnIndexes == null)
        {
            var start = sheet.Dimension.Start;
            var end = sheet.Dimension.End;
            for (int i = start.Row + 1; i <= end.Row; i++)
            {
                sheet.Column(i)?.AutoFit();
            }
        }
        else
        {
            foreach (var index in columnIndexes)
            {
                sheet.Column(index)?.AutoFit();
            }
        }
    }

    public static string GetExcelColumnName(this int columnNumber)
    {
        string columnName = "";

        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }

        return columnName;
    }

    public static string GetExcelColumnName(this string[] columnHeaders, string columnHeader)
    {
        return (Array.IndexOf(columnHeaders, columnHeader) + 1).GetExcelColumnName();
    }

    public static void StyleAsText(this ExcelRange range)
    {
        StyleAs(range, "@");
    }

    public static void StyleAs(this ExcelRange range, string format)
    {
        range.Style.Numberformat.Format = format;
    }
}
