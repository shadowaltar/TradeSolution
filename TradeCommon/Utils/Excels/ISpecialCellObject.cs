namespace TradeCommon.Utils.Excels;
public interface ISpecialCellObject
{
    object? Parse(string? value);

    string[] Formats { get; }

    string PrimaryFormat { get; }
}
