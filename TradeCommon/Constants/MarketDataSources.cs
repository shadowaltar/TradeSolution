using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Constants;
public static class MarketDataSources
{
    public static readonly Dictionary<(SecurityType type, SecurityType subType, ExchangeType exchange), List<string>> SecurityTypeToExternals = new()
    {
        { (SecurityType.Equity, SecurityType.Any, ExchangeType.Hkex), new () { ExternalNames.Futu } },
        { (SecurityType.Fx, SecurityType.Crypto, ExchangeType.Any), new () { ExternalNames.Binance, ExternalNames.Okex } },
    };

    public static List<string>? GetExternalNames(Security security)
    {
        foreach (var (tuple, exchangeNames) in SecurityTypeToExternals)
        {
            (SecurityType t, SecurityType st, ExchangeType ex) = tuple;

            // check type
            var type = SecurityTypeConverter.Parse(security.Type);
            var subType = SecurityTypeConverter.Parse(security.SubType);
            var exchange = ExchangeTypeConverter.Parse(security.SubType);

            var matched = true;

            if (t != SecurityType.Any && t != type)
            {
                matched = false;
            }
            if (st != SecurityType.Any && st != subType)
            {
                matched = false;
            }
            if (ex != ExchangeType.Any && ex != exchange)
            {
                matched = false;
            }
            if (matched)
            {
                return exchangeNames;
            }
        }
        return null;
    }
}
