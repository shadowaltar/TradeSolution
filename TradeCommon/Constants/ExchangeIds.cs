namespace TradeCommon.Constants;

public class ExchangeIds
{
    public static readonly IReadOnlyDictionary<string, int> NameToIds = new Dictionary<string, int>
    {
        { ExternalNames.Binance, 100 },
        { ExternalNames.Hkex, 200 },

        { ExternalNames.Unknown, 0 },
        { ExternalNames.GeneralSimulator, -1 },
    };
}
