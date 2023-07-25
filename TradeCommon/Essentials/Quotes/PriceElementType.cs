using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Essentials.Quotes;
public enum PriceElementType
{
    Default,
    Open,
    High,
    Low,
    Close,
    AdjClose,
    Volume,
    Typical4, // (O+H+L+C)/3
    Typical3, // (H+L+C)/3

    Return, // special type
}

public static class PriceElementTypeConverter
{
    public static PriceElementType Parse(string? str)
    {
        if (str == null)
            return PriceElementType.Default;

        str = str.Trim().ToUpperInvariant();
        return str switch
        {
            "O" or "OPEN" => PriceElementType.Open,
            "H" or "HI" or "HIGH" => PriceElementType.High,
            "L" or "LO" or "LOW" => PriceElementType.Low,
            "C" or "CLOSE" => PriceElementType.Close,
            "AC" or "ADJ-CLOSE" or "ADJCLOSE" => PriceElementType.AdjClose,
            "V" or "VOL" or "VOLUME" => PriceElementType.Volume,
            _ => PriceElementType.Default,
        };
    }
}
