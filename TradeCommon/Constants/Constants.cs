namespace TradeCommon.Constants;
public static class Constants
{
    public const string DefaultDateFormat = "yyyyMMdd";
    public const string DefaultDateTimeFormat = "yyyyMMdd-HHmmss";

    public static readonly string[] YesStrings = { "TRUE", "true", "True", "Yes", "yes", "YES", "T", "Y", "t", "y", "1" };
    public static readonly string[] NoStrings = { "FALSE", "false", "False", "No", "no", "NO", "F", "N", "f", "n", "0" };

    public const int ManualTradingStrategyId = 0;

    public const char SqlCommandPlaceholderPrefix = '$';

    public static readonly string DatabaseFolder = @"C:\Temp\Data";
}
