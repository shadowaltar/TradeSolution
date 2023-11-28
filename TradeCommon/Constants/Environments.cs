using Common;
using TradeCommon.Runtime;

namespace TradeCommon.Constants;
public static class Environments
{
    public const string Unknown = "UNKNOWN";
    public const string Prod = "PROD";
    public const string Uat = "UAT";
    public const string Test = "TEST";
    public const string Simulation = "SIM";

    public static EnvironmentType Parse(string? input)
    {
        if (input.IsBlank())
            return EnvironmentType.Unknown;
        if (input.EqualsIgnoreCase("SIM"))
            return EnvironmentType.Simulation;

        return Enum.TryParse(input, true, out EnvironmentType type) ? type : EnvironmentType.Unknown;
    }

    public static string ToString(EnvironmentType type)
    {
        return type.ToString().ToUpperInvariant();
    }
}