﻿using Common;
using TradeCommon.Constants;

namespace TradeCommon.Essentials.Instruments;
public static class Identifiers
{
    public static string ToFutuCode(Security security)
    {
        return security.Code;
    }

    public static string ToYahooSymbol(string code, string exchange)
    {
        if (exchange == ExternalNames.Hkex)
        {
            return ToYahooSymbolForHK(code);
        }
        return code;
    }

    private static string ToYahooSymbolForHK(string code)
    {
        if (code.IsBlank()) throw new ArgumentNullException(nameof(code));

        if (code.Length == 5 && code.StartsWith("0")) return code[1..] + ".HK";
        return code + ".HK";
    }
}
