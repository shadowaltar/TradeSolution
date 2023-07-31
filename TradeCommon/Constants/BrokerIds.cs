namespace TradeCommon.Constants;
public class BrokerIds
{
    public static readonly IReadOnlyDictionary<string, int> NameToIds = new Dictionary<string, int>
    {
        { ExternalNames.Binance, 100 },
        { ExternalNames.Futu, 200 },

        { ExternalNames.Unknown, 0 },
        { ExternalNames.GeneralSimulator, -1 },
    };
}
