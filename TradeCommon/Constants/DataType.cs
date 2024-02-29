namespace TradeCommon.Constants;

public enum DataType
{
    Unknown,
    OrderBook,
    Tick,
    OhlcPrice,
    FinancialStat,

    Order,
    Trade,
    Asset,
    
    OrderState,
    AssetState,

    Account,
    User,
    AccountLedgerRecord,

    AlgoEntry,
    ServiceTelemetry,
}

public class DataTypeConverter
{
    public static DataType Parse(string? str)
    {
        if (str == null)
            return DataType.Unknown;

        str = str.Trim().ToUpperInvariant();

        return str switch
        {
            "ORDERBOOK" or "DEPTHBOOK" or "ORDERBOOKS" or "DEPTHBOOKS" => DataType.OrderBook,
            "ORDER" or "ORDERS" => DataType.Order,
            "TRADE" or "TRADES" => DataType.Trade,
            "POSITION" or "POSITIONS" => DataType.Position,
            //"TRADEORDERPOSITIONRELATIONSHIP" or "TOP" => DataType.TradeOrderPositionRelationship,
            "FINANCIALSTAT" or "STAT" => DataType.FinancialStat,
            "ACCOUNT" or "ACCOUNTS" => DataType.Account,
            "ASSET" or "ASSETS" => DataType.Asset,
            "USER" or "USERS" => DataType.User,
            _ => DataType.Unknown,
        };
    }

    public static bool Matches(string typeStr, DataType type)
    {
        return Parse(typeStr) == type;
    }
}