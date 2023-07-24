using TradeCommon.Essentials;
using static TradeCommon.Constants.ExternalNames;

namespace TradeCommon.Constants;

public static class ExchangeTypeConverter
{
    public static ExchangeType Parse(string? str)
    {
        if (str == null)
            return ExchangeType.Unknown;

        str = str.Trim().ToUpperInvariant();

        return str switch
        {
            Hkex => ExchangeType.Hkex,
            Binance => ExchangeType.Binance,
            Okex => ExchangeType.Okex,
            _ => ExchangeType.Unknown,
        };
    }
    public static string ToString(ExchangeType exchangeType)
    {
        return exchangeType switch
        {
            ExchangeType.Binance => ExternalNames.Binance,
            ExchangeType.Hkex => ExternalNames.Hkex,
            ExchangeType.Okex => ExternalNames.Okex,
            _ => throw new NotImplementedException(),
        };
    }

    public static bool Matches(string typeStr, ExchangeType type) => Parse(typeStr) == type;
}
