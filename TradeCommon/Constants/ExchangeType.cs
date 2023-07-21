using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Instruments;
using static TradeCommon.Constants.ExternalNames;

namespace TradeCommon.Constants;
public enum ExchangeType
{
    Unknown = 0,
    Any = 0,
    Hkex,
    Binance,
    Okex,
}



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

    public static bool Matches(string typeStr, ExchangeType type) => Parse(typeStr) == type;
}
