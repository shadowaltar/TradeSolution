using log4net;
using OfficeOpenXml;
using OfficeOpenXml.Sorting;
using System.Diagnostics;
using TradeDataCore.Utils;
using TradeDataCore.Utils.Excels;

namespace TradeDataCore.Reporting;
public class ExcelWriter
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ExcelWriter));

    private readonly string _headerBackground = "#AAAAAA";
    private readonly string _headerForeground = "#000000";

    private bool _isDisposed = false;
    private bool _isSavedSuccessfully = false;
    private int _writtenSheetIndex = 0;
    private FileInfo _file = new(Path.GetTempFileName());
    private ExcelPackage? _package;

    public int CurrentWritingSheetIndex => _writtenSheetIndex;

    public ExcelWriter WriteSheet<T>(string columnDefinitionResourcePath, string sheetCaption, List<T> entries)
    {
        if (_isDisposed) throw new InvalidOperationException("The object has been disposed.");

        var columns = ColumnMappingReader.Read(columnDefinitionResourcePath);
        Log.Info($"Start writing sheet {sheetCaption}, with {columns.Count} columns and {entries.Count} rows.");

        _package = new ExcelPackage(_file);
        var sheet = _package.Workbook.Worksheets.Add(sheetCaption);

        var cursor = WriteHeaders(sheet, columns);
        WriteContents(sheet, columns, entries, cursor);

        Log.Info($"Finished writing sheet {sheetCaption}.");
        _writtenSheetIndex++;

        return this;
    }

    public ExcelWriter Save(string outputFilePath)
    {
        if (outputFilePath.IsBlank()) throw new ArgumentNullException(nameof(outputFilePath));
        try
        {
            _package?.Save();
            File.Move(_file.FullName, outputFilePath);
            _package?.Dispose();
            _isDisposed = true;
            _package = null;
            _file = new(outputFilePath);
            Log.Info($"Saved file to {outputFilePath}");
            _isSavedSuccessfully = true;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to generate file {outputFilePath}", e);
        }
        return this;
    }

    public void Open()
    {
        try
        {
            if (!_isSavedSuccessfully)
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = _file.FullName,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to open output file: {_file}", ex);
        }
    }

    protected virtual ExcelRangeBase WriteHeaders(ExcelWorksheet sheet, List<ColumnDefinition> columns, ExcelRangeBase? cursor = null)
    {
        cursor ??= sheet.Cells[1, 1];

        var header = sheet.Cells[1, 1, 1, columns.Count];
        var headerValues = columns.Select(d => d.Caption).ToArray();
        header.SetStyle(isBold: true, background: _headerBackground, foreground: _headerForeground);
        cursor = cursor.WriteRow(headerValues);
        return cursor;
    }

    private ExcelRangeBase WriteContents<T>(ExcelWorksheet sheet, List<ColumnDefinition> columns, List<T> entries, ExcelRangeBase? cursor = null)
    {
        cursor ??= sheet.Cells[1, 1];

        var rowCount = 0;
        foreach (var entry in entries)
        {
            if (entry == null)
            {
                cursor = cursor.Offset(1, 0);
                continue;
            }

            var values = GetRowValuesByReflection(entry, columns);
            cursor = cursor.WriteRow(values);
            rowCount++;
        }
        PostProcessSheet(sheet, columns);

        return cursor;
    }

    /// <summary>
    /// Fill row values by reflection, from properties defined in columns.
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="columns"></param>
    /// <returns></returns>
    protected virtual object?[] GetRowValuesByReflection<T>(T entry, List<ColumnDefinition> columns, Func<string?, string?>? postProcessFormula = null)
    {
        var getters = ReflectionUtils.GetGetterMap<T>();

        entry = entry ?? throw new ArgumentNullException(nameof(entry));
        var values = new object?[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            var cd = columns[i];
            if (!cd.Formula.IsBlank())
            {
                var f = postProcessFormula?.Invoke(cd.Formula) ?? cd.Formula;
                values[i] = ExcelFormula.NewR1C1(f);
            }
            else
            {
                var v = getters[cd.FieldName](entry);
                // handle nullable
                if (v != null && cd.IsNullable)
                {
                    var type = v?.GetType();
                    if (type == typeof(decimal))
                    {
                        v = ((decimal)v!).NullIfZero();
                    }
                    else if (type == typeof(DateTime))
                    {
                        v = ((DateTime)v!).NullIfMin();
                    }
                }
                values[i] = v;
            }
        }
        return values;
    }

    private static void PostProcessSheet(ExcelWorksheet sheet, List<ColumnDefinition> columns)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var cd = columns[i];
            if (cd.Format == null) continue;

            var columnIndex = i + 1;
            var column = sheet.Columns[columnIndex];
            column.Style.Numberformat.Format = cd.Format;
            // set column width to 15 if not specified
            column.Width = cd.Width ?? 15;
        }

        var wholeRange = sheet.Cells[sheet.Dimension.Address];
        wholeRange.Calculate();

        var start = sheet.Dimension.Start;
        var end = sheet.Dimension.End;

        // sort (exclude header)
        var sortDefinitions = new Dictionary<int, (int index, bool isAscending)>();
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].SortIndex == -1) continue;
            sortDefinitions[columns[i].SortIndex] = (i, columns[i].IsAscending);
        }
        var sortingRange = sheet.Cells[2, 1, wholeRange.End.Row, wholeRange.End.Column];
        sortingRange.Sort(x => CreateSortingBuilder(x, sortDefinitions.OrderBy(p => p.Key).Select(p => p.Value).ToList()));
        wholeRange.Calculate();

        // hide columns
        for (int i = columns.Count - 1; i >= 0; i--)
        {
            if (columns[i].IsHidden)
                sheet.Column(i + 1).Hidden = true;
        }
    }

    /// <summary>
    /// Create sorting builder class for EPPlus range sorting from definitions.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="sortDefinitions"></param>
    /// <returns></returns>
    private static RangeSortLayerBuilder? CreateSortingBuilder(RangeSortOptions x, List<(int Index, bool IsAscending)> sortDefinitions)
    {
        if (sortDefinitions.Count == 0) return null;
        (int Index, bool IsAscending) pair = sortDefinitions[0];
        var a = x.SortBy.Column(pair.Index, pair.IsAscending ? eSortOrder.Ascending : eSortOrder.Descending);
        for (int i = 1; i < sortDefinitions.Count; i++)
        {
            pair = sortDefinitions[i];
            a = a.ThenSortBy.Column(pair.Index, pair.IsAscending ? eSortOrder.Ascending : eSortOrder.Descending);
        }
        return a;
    }
}
