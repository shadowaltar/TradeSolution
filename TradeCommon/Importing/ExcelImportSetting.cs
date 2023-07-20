namespace TradeDataCore.Importing;
public class ExcelImportSetting
{
    public string? SheetName { get; set; }
    public int HeaderSkipLineCount { get; set; } = 0;
    public Dictionary<string, object>? HardcodedValues { get; set; }
}
