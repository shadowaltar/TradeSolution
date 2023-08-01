namespace TradeCommon.Constants;

public class ExchangeIds
{
    private static readonly Dictionary<string, int> _nameToIds = new Dictionary<string, int>
    {
        { ExternalNames.Binance.ToUpperInvariant(), 100 },
        { ExternalNames.Hkex.ToUpperInvariant(), 200 },

        { ExternalNames.Unknown.ToUpperInvariant(), 0 },
    };

    public static IReadOnlyDictionary<string, int> NameToIds => _nameToIds;

    public static int GetId(string externalName)
    {
        return _nameToIds.TryGetValue(externalName.ToUpperInvariant(), out var id) ? id : 0;
    }
}
