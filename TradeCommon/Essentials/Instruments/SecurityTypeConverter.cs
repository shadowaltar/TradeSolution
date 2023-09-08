using TradeCommon.Constants;
using static TradeCommon.Constants.SecurityTypes;

namespace TradeCommon.Essentials.Instruments;

public static class SecurityTypeConverter
{
    public static SecurityType Parse(string? str)
    {
        if (str == null)
            return SecurityType.Unknown;

        str = str.Trim().ToUpperInvariant();
        if (StockTypes.Contains(str))
            return SecurityType.Equity;

        return str switch
        {
            Fx or Crypto => SecurityType.Fx,
            Future or Futures => SecurityType.Future,
            Forward => SecurityType.Forward,
            Option => SecurityType.Option,
            _ => SecurityType.Unknown,
        };
    }

    public static SecurityType ParseSubType(string? str)
    {
        if (str == null)
            return SecurityType.Unknown;

        str = str.Trim().ToUpperInvariant();
        if (StockTypes.Contains(str))
            return SecurityType.Equity;

        return str switch
        {
            Crypto => SecurityType.Crypto,
            _ => SecurityType.Unknown,
        };
    }

    public static bool Matches(string typeStr, SecurityType type) => Parse(typeStr) == type;

    public static bool IsEquity(string typeStr) => Matches(typeStr, SecurityType.Equity);
}
