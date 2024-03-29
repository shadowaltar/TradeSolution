﻿using Common;
using Common.Excels;
using log4net;
using OfficeOpenXml;
using OfficeOpenXml.Sorting;
using System.Diagnostics;
using System.Reflection;

namespace TradeCommon.Reporting;
public class ExcelWriter
{
    private static readonly ILog _log = LogManager.GetLogger(typeof(ExcelWriter));

    private readonly string _headerBackground = "#AAAAAA";
    private readonly string _headerForeground = "#000000";

    private bool _isDisposed = false;
    private bool _isSavedSuccessfully = false;
    private FileInfo _file = new(Path.GetTempFileName());
    private ExcelPackage? _package;

    public int CurrentWritingSheetIndex { get; private set; } = 0;

    public ExcelWorksheet CreateSheet(string sheetCaption)
    {
        if (_isDisposed) throw new InvalidOperationException("The object has been disposed.");
        _log.Info($"Start creating sheet {sheetCaption}.");

        _package ??= new ExcelPackage(_file);
        var sheet = _package.Workbook.Worksheets.Add(sheetCaption);
        return sheet;
    }

    public ExcelWriter WriteSheet<T>(string sheetCaption,
                                     IList<T> entries,
                                     bool isActivated = false)
    {
        if (_isDisposed) throw new InvalidOperationException("The object has been disposed.");

        var columns = ColumnMappingReader.Read(typeof(T));
        return WriteSheet<T>(columns, sheetCaption, entries, isActivated);
    }

    public ExcelWriter WriteSheet<T>(string columnDefinitionResourcePath,
                                     string sheetCaption,
                                     IList<T> entries,
                                     bool isActivated = false)
    {
        if (_isDisposed) throw new InvalidOperationException("The object has been disposed.");

        var columns = ColumnMappingReader.Read(columnDefinitionResourcePath);
        return WriteSheet<T>(columns, sheetCaption, entries, isActivated);
    }

    public ExcelWriter WriteSheet<T>(List<ColumnDefinition> columns,
                                     string sheetCaption,
                                     IList<T> entries,
                                     bool isActivated = false)
    {
        if (columns == null) throw new ArgumentNullException(nameof(columns));
        if (_isDisposed) throw new InvalidOperationException("The object has been disposed.");

        var sheet = CreateSheet(sheetCaption);
        _log.Info($"Start writing sheet {sheetCaption}, with {columns.Count} columns and {entries.Count} rows.");

        var cursor = WriteHeaders(sheet, columns);
        WriteContents(sheet, columns, entries, cursor);

        if (isActivated)
            _package!.Workbook.View.ActiveTab = sheet.Index;

        _log.Info($"Finished writing sheet {sheetCaption}.");
        CurrentWritingSheetIndex++;

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
            _log.Info($"Saved file to {outputFilePath}");
            _isSavedSuccessfully = true;
        }
        catch (Exception e)
        {
            _log.Error(e);
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
            _log.Error($"Failed to open output file: {_file}", ex);
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

    private ExcelRangeBase WriteContents<T>(ExcelWorksheet sheet, List<ColumnDefinition> columns, IList<T> entries, ExcelRangeBase? cursor = null)
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
        entry = entry ?? throw new ArgumentNullException(nameof(entry));
        var getter = ReflectionUtils.GetValueGetter<T>();

        var innerObjectColumns = columns.Where(c => c.FieldName.Contains('.')).ToList();
        var innerObjectColumnGroups = innerObjectColumns.GroupBy(ifn => ifn.FieldName.Split('.')[0]);

        var allInnerObjectValueAndFieldNames = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var innerColumnGroup in innerObjectColumnGroups)
        {
            // eg if original property name is "MyObject X {get;set;}" and "class MyObject { int A {get;set;} }"
            // the field name in column definition is like "X.A"
            // the group key will be "X".
            var innerObjectFieldName = innerColumnGroup.Key;
            var (innerV, innerT) = getter.GetTypeAndValue(entry, innerObjectFieldName);
            if (innerV != null)
            {
                allInnerObjectValueAndFieldNames[innerObjectFieldName] = new();
                var innerColumns = innerColumnGroup.Select(ic => ic with { FieldName = RemoveFirstPart('.', ic.FieldName) }).ToList();
                Dictionary<string, PropertyInfo> mapping = ReflectionUtils.GetPropertyToName(innerT);

                // use slow reflection
                for (int i = 0; i < innerColumns.Count; i++)
                {
                    var fn = innerColumns[i].FieldName;
                    var pi = mapping[fn];
                    var value = pi.GetValue(innerV);
                    allInnerObjectValueAndFieldNames[innerObjectFieldName][fn] = value;
                }
            }
        }

        static string RemoveFirstPart(char delimiter, string value)
        {
            var i = value.IndexOf(delimiter);
            return value.Substring(i + 1);
        }


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
                object? v;
                if (cd.FieldName.Contains("."))
                {
                    var parts = cd.FieldName.Split('.');
                    var outerName = parts[0];
                    var innerName = parts[1];
                    var value = allInnerObjectValueAndFieldNames[outerName][innerName];
                    v = value;
                }
                else
                {
                    v = getter.Get(entry, cd.FieldName);
                }
                values[i] = Rectify(v, cd);
            }
        }
        return values;

        static object? Rectify(object value, ColumnDefinition cd)
        {
            if (value != null && cd.IsNullable)
            {
                var type = value?.GetType();
                if (type == typeof(decimal))
                {
                    return ((decimal)value!).NullIfZero()?.NullIfInvalid();
                }
                else if (type == typeof(long))
                {
                    return ((long)value!).NullIfInvalid();
                }
                else if (type == typeof(DateTime))
                {
                    return ((DateTime)value!).NullIfMin();
                }
            }
            return value;
        }
    }

    private static void PostProcessSheet(ExcelWorksheet sheet, List<ColumnDefinition> columns)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var cd = columns[i];
            if (cd.Format == null) continue;

            var columnIndex = i + 1;
            var column = sheet.Columns[columnIndex];
            if (cd.Type != TypeCode.Object)
            {
                column.Style.Numberformat.Format = cd.Format;
            }
            else
            {
                // special case for the non-conventional cell objects
                // use the primary format for the whole column only
                var primary = cd.ConcreteSpecialObject?.PrimaryFormat;
                if (primary != null)
                {
                    column.Style.Numberformat.Format = primary;
                }
            }
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

        if (sortDefinitions.Count != 0)
        {
            var sortingRange = sheet.Cells[2, 1, wholeRange.End.Row, wholeRange.End.Column];
            sortingRange.Sort(x => CreateSortingBuilder(x, sortDefinitions.OrderBy(p => p.Key).Select(p => p.Value).ToList()));
            wholeRange.Calculate();
        }
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
