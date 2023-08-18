using OfficeOpenXml.Style;

namespace TradeCommon.Constants;
public static class ExternalNames
{
    public const string Futu = "Futu";
    public const string Hkex = "HKEX";
    public const string Binance = "Binance";
    public const string Okex = "Okex";
    public const string Yahoo = "Yahoo";

    public const string Unknown = "Unknown";

    public const string Simulator = "Simulator";
    public const string CryptoSimulator = "CryptoSimulator";
    public const string StockSimulator = "StockSimulator";

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
        return externalName.ToUpperInvariant() switch
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
        return externalName.ToUpperInvariant() switch
        {
            Futu => BrokerType.Futu,
            Hkex => BrokerType.Futu,
            Binance => BrokerType.Binance,
            Okex => BrokerType.Okex,
            _ => BrokerType.Unknown,
        };
    }
}
