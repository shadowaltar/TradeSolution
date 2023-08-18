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
        externalName = externalName.ToUpperInvariant();
        if (externalName == Futu.ToUpperInvariant())
        {
            return ExchangeType.Hkex;
        }
        else if (externalName == Hkex.ToUpperInvariant())
        {
            return ExchangeType.Hkex;
        }
        else if (externalName == Binance.ToUpperInvariant())
        {
            return ExchangeType.Binance;
        }
        else if (externalName == Okex.ToUpperInvariant())
        {
            return ExchangeType.Okex;
        }
        return ExchangeType.Unknown;
    }

    public static BrokerType ConvertToBroker(string externalName)
    {
        externalName = externalName.ToUpperInvariant();
        if (externalName == Futu.ToUpperInvariant())
        {
            return BrokerType.Futu;
        }
        else if (externalName == Hkex.ToUpperInvariant())
        {
            return BrokerType.Futu;
        }
        else if (externalName == Binance.ToUpperInvariant())
        {
            return BrokerType.Binance;
        }
        else if (externalName == Okex.ToUpperInvariant())
        {
            return BrokerType.Okex;
        }
        return BrokerType.Unknown;
    }

    public static int GetBrokerId(string externalName)
    {
        var type = ConvertToBroker(externalName);
        return BrokerTypeToIds.GetValueOrDefault(type, BrokerTypeToIds[BrokerType.Unknown]);
    }
}
