using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Constants;
public static class Consts
{
    public const string DefaultDateFormat = "yyyyMMdd";
    public const string DefaultDateTimeFormat = "yyyyMMdd-HHmmss";

    public static readonly string[] YesStrings = { "TRUE", "true", "True", "Yes", "yes", "YES", "T", "Y", "t", "y", "1" };
    public static readonly string[] NoStrings = { "FALSE", "false", "False", "No", "no", "NO", "F", "N", "f", "n", "0" };

    public const int PasswordMinLength = 6;

    public const int DefaultStrategyId = 0;

    public const int DefaultOrderBookDepth = 5;

    public const int ManualTradingStrategyId = 1;

    public const int LookbackDayCount = 30;

    public const char SqlCommandPlaceholderPrefix = '$';

    public static readonly string DatabaseFolder = @"C:\Temp\Data";

    public static readonly int OrderBookLevels = 5;

    public const int EnumDatabaseTypeSize = 20;

    public const decimal StopPriceRatio = 0.4m;

    public static readonly SecurityType[] SupportedSecurityTypes = new[] {
        SecurityType.Fx,
        SecurityType.Equity,
    };
}
