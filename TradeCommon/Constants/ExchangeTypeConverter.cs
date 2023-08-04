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

        if (str.EqualsIgnoreCase(Hkex))
        {
            return ExchangeType.Hkex;
        }
        if (str.EqualsIgnoreCase(Binance))
        {
            return ExchangeType.Binance;
        }
        if (str.EqualsIgnoreCase(Okex))
        {
            return ExchangeType.Okex;
        }
        return ExchangeType.Unknown;
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
