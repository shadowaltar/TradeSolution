using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Database;

public static class DatabaseNames
{
    public const string StaticData = "static_data";
    public const string MarketData = "market_data";
    public const string ExecutionData = "execution_data";

    public const string AccountTable = "accounts";
    public const string BalanceTable = "balances";
    public const string UserTable = "users";

    public const string StockDefinitionTable = "stock_definitions";
    public const string FxDefinitionTable = "fx_definitions";

    public const string FinancialStatsTable = "financial_stats";

    public const string StockPrice1mTable = "stock_prices_1m";
    public const string StockPrice1hTable = "stock_prices_1h";
    public const string StockPrice1dTable = "stock_prices_1d";
    public const string FxPrice1mTable = "fx_prices_1m";
    public const string FxPrice1hTable = "fx_prices_1h";
    public const string FxPrice1dTable = "fx_prices_1d";

    public const string StockOrderTable = "stock_orders";
    public const string StockTradeTable = "stock_trades";
    public const string StockPositionTable = "stock_positions";
    public const string FxOrderTable = "fx_orders";
    public const string FxTradeTable = "fx_trades";
    public const string FxPositionTable = "fx_positions";
    public const string ErrorStockOrderTable = "error_stock_orders";
    public const string ErrorStockTradeTable = "error_stock_trades";
    public const string ErrorStockPositionTable = "error_stock_positions";
    public const string ErrorFxOrderTable = "error_fx_orders";
    public const string ErrorFxTradeTable = "error_fx_trades";
    public const string ErrorFxPositionTable = "error_fx_positions";

    public const string StockTradeToOrderToPositionIdTable = "stock_trade_order_position_ids";
    public const string FxTradeToOrderToPositionIdTable = "fx_trade_order_position_ids";

    public static string GetDefinitionTableName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Equity => StockDefinitionTable,
            SecurityType.Fx => FxDefinitionTable,
            _ => "",
        };
    }

    public static string GetOrderTableName(SecurityType type, bool isErrorTable = false)
    {
        if (!isErrorTable)
            return type switch
            {
                SecurityType.Equity => StockOrderTable,
                SecurityType.Fx => FxOrderTable,
                _ => throw new NotImplementedException()
            };
        else
            return type switch
            {
                SecurityType.Equity => ErrorStockOrderTable,
                SecurityType.Fx => ErrorFxOrderTable,
                _ => throw new NotImplementedException()
            };
    }

    public static string GetTradeTableName(SecurityType type, bool isErrorTable = false)
    {
        if (!isErrorTable)
            return type switch
            {
                SecurityType.Equity => StockTradeTable,
                SecurityType.Fx => FxTradeTable,
                _ => throw new NotImplementedException()
            };
        else
            return type switch
            {
                SecurityType.Equity => ErrorStockTradeTable,
                SecurityType.Fx => ErrorFxTradeTable,
                _ => throw new NotImplementedException()
            };
    }

    public static string GetPositionTableName(SecurityType type, bool isErrorTable = false)
    {
        if (!isErrorTable)
            return type switch
            {
                SecurityType.Equity => StockPositionTable,
                SecurityType.Fx => FxPositionTable,
                _ => throw new NotImplementedException()
            };
        else
            return type switch
            {
                SecurityType.Equity => ErrorStockPositionTable,
                SecurityType.Fx => ErrorFxPositionTable,
                _ => throw new NotImplementedException()
            };
    }

    public static string GetPriceTableName(IntervalType intervalType, SecurityType securityType)
    {
        string tableName;
        switch (intervalType)
        {
            case IntervalType.OneDay:
                tableName = securityType == SecurityType.Equity
                    ? StockPrice1dTable
                    : securityType == SecurityType.Fx
                    ? FxPrice1dTable
                    : throw new NotImplementedException();
                break;
            case IntervalType.OneHour:
                tableName = securityType == SecurityType.Equity
                    ? StockPrice1hTable
                    : securityType == SecurityType.Fx
                    ? FxPrice1hTable
                    : throw new NotImplementedException();
                break;
            case IntervalType.OneMinute:
                tableName = securityType == SecurityType.Equity
                    ? StockPrice1mTable
                    : securityType == SecurityType.Fx
                    ? FxPrice1mTable
                    : throw new NotImplementedException();
                break;
            default: throw new NotImplementedException();
        }
        return tableName;
    }


    public static string GetTradeOrderPositionIdTable(SecurityType type)
    {
        return type switch
        {
            SecurityType.Equity => StockTradeToOrderToPositionIdTable,
            SecurityType.Fx => FxTradeToOrderToPositionIdTable,
            _ => throw new NotImplementedException()
        };
    }
}
