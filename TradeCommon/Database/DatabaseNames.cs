using Common;
using Common.Attributes;
using System.Reflection;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Database;

public static class DatabaseNames
{
    public const string DatabaseSuffix = "_data";

    public const string AlgorithmData = "algorithm_data";
    public const string StaticData = "static_data";
    public const string MarketData = "market_data";
    public const string ExecutionData = "execution_data";

    public const string AccountTable = "accounts";
    public const string AssetTable = "assets";
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
    public const string StockOrderStateTable = "stock_order_states";
    public const string StockAssetStateTable = "stock_asset_states";
    public const string FxOrderTable = "fx_orders";
    public const string FxTradeTable = "fx_trades";
    public const string FxPositionTable = "fx_positions";
    public const string FxOrderStateTable = "fx_order_states";
    public const string FxAssetStateTable = "fx_asset_states";
    public const string ErrorStockOrderTable = "error_stock_orders";
    public const string ErrorStockTradeTable = "error_stock_trades";
    public const string ErrorStockPositionTable = "error_stock_positions";
    public const string ErrorFxOrderTable = "error_fx_orders";
    public const string ErrorFxTradeTable = "error_fx_trades";
    public const string ErrorFxPositionTable = "error_fx_positions";

    public const string StockTradeToOrderToPositionIdTable = "stock_trade_order_position_ids";
    public const string FxTradeToOrderToPositionIdTable = "fx_trade_order_position_ids";

    public const string TradePositionReconciliation = "reconciled_trade_position";

    private static readonly Dictionary<Type, (string tableName, string databaseName)> _tableDatabaseNamesByType = new();

    public static string GetDatabaseName<T>()
    {
        var t = typeof(T);
        if (t == typeof(Trade)) return ExecutionData;
        if (t == typeof(Order)) return ExecutionData;
        if (t == typeof(OrderState)) return ExecutionData;
        if (t == typeof(Position)) return ExecutionData;
        if (t == typeof(Asset)) return ExecutionData;
        if (t == typeof(Asset)) return ExecutionData;

        if (t == typeof(Account)) return StaticData;
        if (t == typeof(Tick)) return MarketData;
        if (t == typeof(OhlcPrice)) return MarketData;
        if (t == typeof(ExtendedOhlcPrice)) return MarketData;
        throw new InvalidOperationException("Unsupported Type to Database name mapping.");
    }

    public static string GetDefinitionTableName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Equity => StockDefinitionTable,
            SecurityType.Fx => FxDefinitionTable,
            _ => "",
        };
    }

    public static string? GetOrderTableName(SecurityType type, bool isErrorTable = false)
    {
        return !isErrorTable
            ? type switch
            {
                SecurityType.Equity => StockOrderTable,
                SecurityType.Fx => FxOrderTable,
                _ => null,
            }
            : type switch
            {
                SecurityType.Equity => ErrorStockOrderTable,
                SecurityType.Fx => ErrorFxOrderTable,
                _ => null,
            };
    }

    public static string? GetOrderTableName(string securityType, bool isErrorTable = false)
    {
        var type = SecurityTypeConverter.Parse(securityType);
        return GetOrderTableName(type, isErrorTable);
    }

    public static string? GetOrderStateTableName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Equity => StockOrderStateTable,
            SecurityType.Fx => FxOrderStateTable,
            _ => null,
        };
    }

    public static string? GetTradeTableName(SecurityType type, bool isErrorTable = false)
    {
        return !isErrorTable
            ? type switch
            {
                SecurityType.Equity => StockTradeTable,
                SecurityType.Fx => FxTradeTable,
                _ => null,
            }
            : type switch
            {
                SecurityType.Equity => ErrorStockTradeTable,
                SecurityType.Fx => ErrorFxTradeTable,
                _ => null,
            };
    }

    public static string? GetTradeTableName(string securityType, bool isErrorTable = false)
    {
        var type = SecurityTypeConverter.Parse(securityType);
        return GetTradeTableName(type, isErrorTable);
    }

    public static string? GetAssetStateTableName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Equity => StockPositionTable,
            SecurityType.Fx => FxPositionTable,
            _ => null
        };
    }

    public static string? GetPositionTableName(SecurityType type, bool isErrorTable = false)
    {
        return !isErrorTable
            ? type switch
            {
                SecurityType.Equity => StockPositionTable,
                SecurityType.Fx => FxPositionTable,
                _ => null
            }
            : type switch
            {
                SecurityType.Equity => ErrorStockPositionTable,
                SecurityType.Fx => ErrorFxPositionTable,
                _ => null
            };
    }

    public static string? GetPositionTableName(string securityType, bool isErrorTable = false)
    {
        var type = SecurityTypeConverter.Parse(securityType);
        return GetPositionTableName(type, isErrorTable);
    }

    public static string GetPriceTableName(IntervalType intervalType, SecurityType securityType)
    {
        string tableName = intervalType switch
        {
            IntervalType.OneDay => securityType == SecurityType.Equity
                                ? StockPrice1dTable
                                : securityType == SecurityType.Fx
                                ? FxPrice1dTable
                                : throw new NotImplementedException(),
            IntervalType.OneHour => securityType == SecurityType.Equity
                                ? StockPrice1hTable
                                : securityType == SecurityType.Fx
                                ? FxPrice1hTable
                                : throw new NotImplementedException(),
            IntervalType.OneMinute => securityType == SecurityType.Equity
                                ? StockPrice1mTable
                                : securityType == SecurityType.Fx
                                ? FxPrice1mTable
                                : throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
        return tableName;
    }

    public static string GetOrderBookTableName(string securityCode, ExchangeType exchange, int level)
    {
        return $"order_book_{level}_{exchange.ToString().ToLower()}_{securityCode.ToLower()}";
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

    public static (string tableName, string databaseName) GetTableAndDatabaseName<T>(SecurityType securityType) where T : class
    {
        var type = typeof(T);
        var specificTable = "";
        if (type == typeof(Order))
        {
            specificTable = GetOrderTableName(securityType);
        }
        else if (type == typeof(Trade))
        {
            specificTable = GetTradeTableName(securityType);
        }
        else if (type == typeof(Position))
        {
            specificTable = GetPositionTableName(securityType);
        }
        else if (type == typeof(OrderState))
        {
            specificTable = GetOrderStateTableName(securityType);
        }

        (string tableName, string databaseName) = GetTableAndDatabaseName<T>();
        specificTable = specificTable.IsBlank() ? tableName : specificTable;
        return (specificTable, databaseName);
    }

    public static (string tableName, string databaseName) GetTableAndDatabaseName<T>(T entry) where T : class
    {
        var specificTable = "";
        if (entry is SecurityRelatedEntry sre)
        {
            var secType = sre.Security?.SecurityType ?? throw Exceptions.Invalid<Order>($"Missing security in {entry.GetType().Name}.");
            if (entry is Order)
            {
                specificTable = GetOrderTableName(secType);
            }
            else if (entry is Trade)
            {
                specificTable = GetTradeTableName(secType);
            }
            else if (entry is OrderState)
            {
                specificTable = GetOrderStateTableName(secType);
            }
            else if (entry is Position)
            {
                specificTable = GetPositionTableName(secType);
            }
        }
        (string tableName, string databaseName) = GetTableAndDatabaseName<T>();
        specificTable = specificTable.IsBlank() ? tableName : specificTable;
        return (specificTable, databaseName);
    }

    public static (string tableName, string databaseName) GetTableAndDatabaseName<T>() where T : class
    {
        var type = typeof(T);
        if (!_tableDatabaseNamesByType.TryGetValue(type, out (string tableName, string databaseName) existing))
        {
            var storageAttr = type.GetCustomAttribute<StorageAttribute>() ?? throw Exceptions.InvalidStorageDefinition();

            var table = storageAttr.TableName;
            var database = storageAttr.DatabaseName;
            if (table.IsBlank() || database.IsBlank()) throw Exceptions.InvalidStorageDefinition();
            if (!database.EndsWith(DatabaseSuffix))
                database += DatabaseSuffix;
            existing = (table, database);
            _tableDatabaseNamesByType[type] = existing;
        }
        return existing;
    }
}
