using TradeCommon.Utils.Excels;

namespace TradeCommon.Reporting;

public record ColumnDefinition
{
    public required int Index { get; set; }
    public required string FieldName { get; set; }
    public required string Caption { get; set; }
    public required TypeCode Type { get; set; }
    public string? Format { get; set; }
    public string CellReference => "RC" + (Index + 1);
    public required bool IsNullable { get; set; } = false;
    public string? Formula { get; set; }
    public int? Width { get; set; }
    public int SortIndex { get; set; } = -1;
    public bool IsAscending { get; set; }
    public bool IsHidden { get; set; } = false;

    public bool IsSpecialObjectColumn => Type == TypeCode.Object;

    /// <summary>
    /// Special object type column only. It converts the value into the object.
    /// Eg, if in column definition the Type's name is ClassA, then
    /// ClassA must implement <see cref="ISpecialCellObject"/>.
    /// </summary>
    public ISpecialCellObject? ConcreteSpecialObject { get; set; }
}
