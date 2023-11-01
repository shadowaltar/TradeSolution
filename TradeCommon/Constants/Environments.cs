using Common;
using TradeCommon.Runtime;

namespace TradeCommon.Constants;
public static class Environments
{
    public const string Unknown = "UNKNOWN";
    public const string Prod = "PROD";
    public const string Test = "TEST";
    public const string Uat = "UAT";

    public static EnvironmentType Parse(string? input)
    {
        return input.IsBlank()
            ? EnvironmentType.Unknown
            : Enum.TryParse(input, true, out EnvironmentType type) ? type : EnvironmentType.Unknown;
    }

    public static string ToString(EnvironmentType type)
    {
        return type.ToString().ToUpperInvariant();
    }
}