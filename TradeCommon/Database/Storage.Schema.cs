using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials;

namespace TradeCommon.Database;

public partial class Storage
{
    public static async Task CreateSecurityTable(SecurityType type)
    {
        if (type == SecurityType.Equity)
        {
            await CreateStockDefinitionTable();
        }
        else if (type == SecurityType.Fx)
        {
            await CreateFxDefinitionTable();
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private static async Task CreateStockDefinitionTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.StockDefinitionTable};
DROP INDEX IF EXISTS idx_code_exchange;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.StockDefinitionTable} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code VARCHAR(100) NOT NULL,
    Name VARCHAR(400),
    Exchange VARCHAR(100) NOT NULL,
    Type VARCHAR(100) NOT NULL,
    SubType VARCHAR(200),
    LotSize DOUBLE DEFAULT 1 NOT NULL,
    Currency CHAR(3) NOT NULL,
    Cusip VARCHAR(100),
    Isin VARCHAR(100),
    YahooTicker VARCHAR(100),
    IsShortable BOOLEAN DEFAULT FALSE,
    IsEnabled BOOLEAN DEFAULT TRUE,
    LocalStartDate DATE NOT NULL DEFAULT 0, 
    LocalEndDate DATE NOT NULL,
    UNIQUE(Code, Exchange)
);
CREATE UNIQUE INDEX idx_code_exchange
    ON {DatabaseNames.StockDefinitionTable} (Code, Exchange);
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
    }

    private static async Task CreateFxDefinitionTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.FxDefinitionTable};
DROP INDEX IF EXISTS idx_code_exchange;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.FxDefinitionTable} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code VARCHAR(100) NOT NULL,
    Name VARCHAR(400),
    Exchange VARCHAR(100) NOT NULL,
    Type VARCHAR(100) NOT NULL,
    SubType VARCHAR(200),
    LotSize DOUBLE DEFAULT 1 NOT NULL,
    Currency VARCHAR(20) NOT NULL,
    BaseCurrency VARCHAR(10) NOT NULL,
    QuoteCurrency VARCHAR(10) NOT NULL,
    IsEnabled BOOLEAN DEFAULT TRUE,
    LocalStartDate DATE NOT NULL DEFAULT 0, 
    LocalEndDate DATE NOT NULL,
    UNIQUE(Code, BaseCurrency, QuoteCurrency, Exchange)
);
CREATE UNIQUE INDEX idx_code_exchange
    ON {DatabaseNames.FxDefinitionTable} (Code, Exchange);
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {DatabaseNames.FxDefinitionTable} table in {DatabaseNames.StaticData}.");
    }

    public static async Task CreatePriceTable(IntervalType interval, SecurityType securityType)
    {
        var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
        string dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_sec_start;
DROP INDEX IF EXISTS idx_{tableName}_sec;
";
        var dailyPriceSpecificColumn = securityType == SecurityType.Equity && interval == IntervalType.OneDay ? "AdjClose REAL NOT NULL," : "";
        string createSql =
@$"
CREATE TABLE IF NOT EXISTS {tableName} (
    SecurityId INT NOT NULL,
    Open REAL NOT NULL,
    High REAL NOT NULL,
    Low REAL NOT NULL,
    Close REAL NOT NULL,
    {dailyPriceSpecificColumn}
    Volume REAL NOT NULL,
    StartTime INT NOT NULL,
    UNIQUE(SecurityId, StartTime)
);
CREATE UNIQUE INDEX idx_{tableName}_sec_start
ON {tableName} (SecurityId, StartTime);
CREATE INDEX idx_{tableName}_sec
ON {tableName} (SecurityId);
";
        using var connection = await Connect(DatabaseNames.MarketData);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {tableName} table in {DatabaseNames.MarketData}.");
    }

    public static async Task<List<string>> CreateOrderTable(SecurityType securityType)
    {
        var tableNames = new List<string>
        {
            DatabaseNames.GetOrderTableName(securityType),
            DatabaseNames.GetOrderTableName(securityType, true)
        };

        foreach (var tableName in tableNames)
        {
            var dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_id;
DROP INDEX IF EXISTS idx_{tableName}_securityId;
DROP INDEX IF EXISTS idx_{tableName}_securityId_createTime;
DROP INDEX IF EXISTS idx_{tableName}_externalOrderId;
";
            var createSql =
    @$"
CREATE TABLE IF NOT EXISTS {tableName} (
    Id INTEGER PRIMARY KEY,
    ExternalOrderId INTEGER NOT NULL,
    SecurityId INTEGER NOT NULL,
    AccountId INTEGER NOT NULL,
    Type VARCHAR(40) NOT NULL,
    Price DOUBLE NOT NULL,
    Quantity DOUBLE NOT NULL,
    Side CHAR(1) NOT NULL,
    StopPrice DOUBLE DEFAULT 0 NOT NULL,
    CreateTime INT NOT NULL,
    UpdateTime INT NOT NULL,
    ExternalCreateTime INT DEFAULT 0,
    ExternalUpdateTime INT DEFAULT 0,
    TimeInForce VARCHAR(10),
    StrategyId INT DEFAULT 0,
    BrokerId INT DEFAULT 0,
    ExchangeId INT DEFAULT 0,
    UNIQUE(Id)
);
CREATE INDEX idx_{tableName}_id
    ON {tableName} (Id);
CREATE INDEX idx_{tableName}_securityId
    ON {tableName} (SecurityId);
CREATE UNIQUE INDEX idx_{tableName}_securityId_createTime
    ON {tableName} (SecurityId, CreateTime);
CREATE INDEX idx_{tableName}_externalOrderId
    ON {tableName} (ExternalOrderId);
";
            using var connection = await Connect(DatabaseNames.ExecutionData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            _log.Info($"Created {tableName} table in {DatabaseNames.ExecutionData}.");
        }

        return tableNames;
    }

    public static async Task<List<string>> CreateTradeTable(SecurityType securityType)
    {
        var tableNames = new List<string>
        {
            DatabaseNames.GetTradeTableName(securityType),
            DatabaseNames.GetTradeTableName(securityType, true)
        };

        foreach (var tableName in tableNames)
        {
            var dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_securityId;
DROP INDEX IF EXISTS idx_{tableName}_securityId_time;
";
            var createSql =
    @$"
CREATE TABLE IF NOT EXISTS {tableName} (
    Id INTEGER PRIMARY KEY,
    SecurityId INTEGER NOT NULL,
    ExternalTradeId INTEGER NOT NULL,
    ExternalOrderId INTEGER NOT NULL,
    Time INT NOT NULL,
    Type VARCHAR(40) NOT NULL,
    Price DOUBLE NOT NULL,
    Quantity DOUBLE NOT NULL,
    Side CHAR(1) NOT NULL,
    Fee DOUBLE NOT NULL,
    BrokerId INT DEFAULT 0,
    ExchangeId INT DEFAULT 0,
    UNIQUE(Id)
);
CREATE UNIQUE INDEX idx_{tableName}_id
    ON {tableName} (Id);
CREATE UNIQUE INDEX idx_{tableName}_securityId
    ON {tableName} (SecurityId);
CREATE UNIQUE INDEX idx_{tableName}_securityId_time
    ON {tableName} (SecurityId, Time);
";
            using var connection = await Connect(DatabaseNames.ExecutionData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            _log.Info($"Created {tableName} table in {DatabaseNames.ExecutionData}.");
        }

        return tableNames;
    }

    public static async Task<List<string>> CreatePositionTable(SecurityType securityType)
    {
        var tableNames = new List<string>
        {
            DatabaseNames.GetPositionTableName(securityType),
            DatabaseNames.GetPositionTableName(securityType, true)
        };

        foreach (var tableName in tableNames)
        {
            var dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_securityId;
DROP INDEX IF EXISTS idx_{tableName}_securityId_time;
";
            var createSql =
    @$"
CREATE TABLE IF NOT EXISTS {tableName} (
    Id INTEGER PRIMARY KEY,
    SecurityId INTEGER NOT NULL,
    StartTime INT NOT NULL,
    UpdateTime INT NOT NULL,
    Quantity DOUBLE NOT NULL,
    Price DOUBLE NOT NULL,
    Currency VARCHAR(10),
    RealizedPnl DOUBLE NOT NULL,
    UNIQUE(Id)
);
CREATE UNIQUE INDEX idx_{tableName}_securityId
    ON {tableName} (SecurityId);
CREATE UNIQUE INDEX idx_{tableName}_securityId_startTime
    ON {tableName} (SecurityId, StartTime);
";
            using var connection = await Connect(DatabaseNames.ExecutionData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            _log.Info($"Created {tableName} table in {DatabaseNames.ExecutionData}.");
        }

        return tableNames;
    }

    public static async Task<string> CreateTradeOrderPositionIdTable(SecurityType securityType)
    {
        var tableName = DatabaseNames.GetTradeOrderPositionIdTable(securityType);
        var dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_securityId;
DROP INDEX IF EXISTS idx_{tableName}_tradeId;
DROP INDEX IF EXISTS idx_{tableName}_orderId;
DROP INDEX IF EXISTS idx_{tableName}_positionId;
DROP INDEX IF EXISTS idx_{tableName}_externalTradeId;
DROP INDEX IF EXISTS idx_{tableName}_externalOrderId;
";
        var createSql =
@$"
CREATE TABLE IF NOT EXISTS {tableName} (
    Id INTEGER PRIMARY KEY,
    SecurityId INTEGER NOT NULL,
    TradeId INTEGER NOT NULL,
    OrderId INTEGER NOT NULL,
    PositionId INTEGER NOT NULL,
    ExternalTradeId INTEGER,
    ExternalOrderId INTEGER,
    UNIQUE(Id)
);
CREATE UNIQUE INDEX idx_{tableName}_securityId
    ON {tableName} (SecurityId);
CREATE UNIQUE INDEX idx_{tableName}_tradeId
    ON {tableName} (TradeId);
CREATE UNIQUE INDEX idx_{tableName}_orderId
    ON {tableName} (OrderId);
CREATE UNIQUE INDEX idx_{tableName}_positionId
    ON {tableName} (PositionId);
CREATE UNIQUE INDEX idx_{tableName}_externalTradeId
    ON {tableName} (ExternalTradeId);
CREATE UNIQUE INDEX idx_{tableName}_externalOrderId
    ON {tableName} (ExternalOrderId);
";
        using var connection = await Connect(DatabaseNames.ExecutionData);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {tableName} table in {DatabaseNames.ExecutionData}.");


        return tableName;
    }

    public static async Task CreateFinancialStatsTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.FinancialStatsTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.FinancialStatsTable}_securityId;
";
        const string createSql =
@$"CREATE TABLE IF NOT EXISTS {DatabaseNames.FinancialStatsTable} (
    SecurityId INT NOT NULL,
    MarketCap REAL NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX idx_{DatabaseNames.FinancialStatsTable}_securityId
ON {DatabaseNames.FinancialStatsTable} (SecurityId);
";
        using var connection = await Connect(DatabaseNames.StaticData);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {DatabaseNames.FinancialStatsTable} table in {DatabaseNames.StaticData}.");
    }
}
