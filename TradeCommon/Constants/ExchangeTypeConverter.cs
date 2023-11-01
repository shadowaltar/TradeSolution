using Common;
using static TradeCommon.Constants.ExternalNames;

namespace TradeCommon.Constants;

public static class ExchangeTypeConverter
{
    public static ExchangeType Parse(string? str)
    {
        if (str == null)
            return ExchangeType.Unknown;

        str = str.Trim().ToUpperInvariant();
        if (str.EqualsIgnoreCase(Binance))
        {
            return str.EqualsIgnoreCase(Hkex)
                ? ExchangeType.Hkex
                : ExchangeType.Binance;
        }
        else
        {
            return str.EqualsIgnoreCase(Okex)
                ? str.EqualsIgnoreCase(Hkex)
                ? ExchangeType.Hkex
                : ExchangeType.Okex
                : str.EqualsIgnoreCase(Hkex)
                ? ExchangeType.Hkex
                : ExchangeType.Unknown;
        }
    }

    public static string ToString(ExchangeType exchangeType)
    {
        return exchangeType switch
        {
            ExchangeType.Binance => Binance,
            ExchangeType.Hkex => Hkex,
            ExchangeType.Okex => Okex,
            _ => Unknown,
        };
    }

    public static bool Matches(string typeStr, ExchangeType type)
    {
        return Parse(typeStr) == type;
    }
}
