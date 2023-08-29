using OfficeOpenXml.Style;

namespace TradeCommon.Constants;
public static class ExternalNames
{
    public const string Futu = "FUTU";
    public const string Hkex = "HKEX";
    public const string Binance = "BINANCE";
    public const string Okex = "OKEX";
    public const string Yahoo = "YAHOO";

    public const string Unknown = "UNKNOWN";

    public const string Simulator = "SIMULATOR";

    public static readonly IReadOnlyDictionary<BrokerType, int> BrokerTypeToIds = new Dictionary<BrokerType, int>()
    {
        { BrokerType.Binance, 100 },
        { BrokerType.Futu, 200 },

        { BrokerType.Unknown, 0 },
        { BrokerType.Simulator, -1 },
    };

    public static BrokerType Convert(ExchangeType exchangeType)
    {
        return exchangeType switch
        {
            ExchangeType.Unknown => BrokerType.Unknown,
            ExchangeType.Hkex => BrokerType.Futu,
            ExchangeType.Binance => BrokerType.Binance,
            ExchangeType.Okex => BrokerType.Okex,
            _ => BrokerType.Unknown,
        };
    }

    public static ExchangeType Convert(BrokerType brokerType)
    {
        return brokerType switch
        {
            BrokerType.Unknown => ExchangeType.Unknown,
            BrokerType.Futu => ExchangeType.Hkex,
            BrokerType.Binance => ExchangeType.Binance,
            BrokerType.Okex => ExchangeType.Okex,
            _ => ExchangeType.Unknown,
        };
    }

    public static ExchangeType ConvertToExchange(string externalName)
    {
        externalName = externalName.ToUpperInvariant();
        return externalName switch
        {
            Futu => ExchangeType.Hkex,
            Hkex => ExchangeType.Hkex,
            Binance => ExchangeType.Binance,
            Okex => ExchangeType.Okex,
            _ => ExchangeType.Unknown,
        };
    }

    public static BrokerType ConvertToBroker(string externalName)
    {
        externalName = externalName.ToUpperInvariant();
        return externalName switch
        {
            Futu => BrokerType.Futu,
            Hkex => BrokerType.Futu,
            Binance => BrokerType.Binance,
            Okex => BrokerType.Okex,
            _ => BrokerType.Unknown,
        };
    }

    public static int GetBrokerId(string externalName)
    {
        var type = ConvertToBroker(externalName);
        return BrokerTypeToIds.GetValueOrDefault(type, BrokerTypeToIds[BrokerType.Unknown]);
    }

    public static int GetBrokerId(BrokerType broker)
    {
        return BrokerTypeToIds.GetValueOrDefault(broker, BrokerTypeToIds[BrokerType.Unknown]);
    }
}
