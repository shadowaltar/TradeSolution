using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Database;

public partial class Storage
{
    public static async Task CreateUserTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.UserTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.UserTable}_name;
DROP INDEX IF EXISTS idx_{DatabaseNames.UserTable}_email;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.UserTable} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL,
    EncryptedPassword VARCHAR(512) NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE,
    UNIQUE(Name)
);
CREATE UNIQUE INDEX idx_{DatabaseNames.UserTable}_name
    ON {DatabaseNames.UserTable} (Name);
CREATE UNIQUE INDEX idx_{DatabaseNames.UserTable}_email
    ON {DatabaseNames.UserTable} (Email);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.UserTable, DatabaseNames.StaticData);
    }

    public static async Task CreateAccountTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.AccountTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.AccountTable}_name_exchange;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.AccountTable} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OwnerId INT NOT NULL,
    Name VARCHAR(100) NOT NULL,
    BrokerId INT NOT NULL,
    ExternalAccountId INT,
    Type VARCHAR(100),
    SubType VARCHAR(100),
    Environment VARCHAR(10) NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE,
    UNIQUE(Name, Exchange, Environment)
);
CREATE UNIQUE INDEX idx_{DatabaseNames.AccountTable}_name_exchange
    ON {DatabaseNames.AccountTable} (Name, Exchange);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.AccountTable, DatabaseNames.StaticData);
    }

    public static async Task CreateBalanceTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.BalanceTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.BalanceTable}_accountId;
DROP INDEX IF EXISTS idx_{DatabaseNames.BalanceTable}_assetId;
DROP INDEX IF EXISTS idx_{DatabaseNames.BalanceTable}_accountId_assetId;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.BalanceTable} (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AssetId INT NOT NULL,
    AccountId INT NOT NULL,
    FreeAmount REAL DEFAULT 0 NOT NULL,
    LockedAmount REAL DEFAULT 0 NOT NULL,
    UpdateTime DATE,
    UNIQUE(AssetId, AccountId)
);
CREATE UNIQUE INDEX idx_{DatabaseNames.BalanceTable}_accountId_assetId
    ON {DatabaseNames.BalanceTable} (AssetId, AccountId);
CREATE INDEX idx_{DatabaseNames.BalanceTable}_accountId
    ON {DatabaseNames.BalanceTable} (AccountId);
CREATE INDEX idx_{DatabaseNames.BalanceTable}_assetId
    ON {DatabaseNames.BalanceTable} (AssetId);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.BalanceTable, DatabaseNames.StaticData);
    }

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
        await DropThenCreate(dropSql, createSql, DatabaseNames.StockDefinitionTable, DatabaseNames.StaticData);
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
    LotSize DOUBLE NOT NULL,
    BaseCurrency VARCHAR(10) NOT NULL,
    QuoteCurrency VARCHAR(10) NOT NULL,
    IsEnabled BOOLEAN DEFAULT TRUE,
    IsMarginTradingAllowed BOOLEAN DEFAULT TRUE,
    LocalStartDate DATE NOT NULL DEFAULT 0, 
    LocalEndDate DATE NOT NULL,
    MaxLotSize DOUBLE,
    MinNotional DOUBLE,
    PricePrecision DOUBLE,
    QuotePrecision DOUBLE,
    UNIQUE(Code, BaseCurrency, QuoteCurrency, Exchange)
);
CREATE UNIQUE INDEX idx_code_exchange
    ON {DatabaseNames.FxDefinitionTable} (Code, Exchange);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.FxDefinitionTable, DatabaseNames.StaticData);
    }

    public static async Task<string> CreatePriceTable(IntervalType interval, SecurityType securityType)
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

        await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.MarketData);
        return tableName;
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
    Price REAL NOT NULL,
    Quantity REAL NOT NULL,
    FilledQuantity REAL NOT NULL,
    Status VARCHAR(10) NOT NULL,
    Side CHAR(1) NOT NULL,
    StopPrice REAL DEFAULT 0 NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE NOT NULL,
    ExternalCreateTime DATE DEFAULT 0,
    ExternalUpdateTime DATE DEFAULT 0,
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

            await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.ExecutionData);
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

            await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.ExecutionData);
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

            await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.ExecutionData);
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

        await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.ExecutionData);

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

        await DropThenCreate(dropSql, createSql, DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData);
    }

    private static async Task DropThenCreate(string dropSql, string createSql, string tableName, string databaseName)
    {
        using var connection = await Connect(databaseName);

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = dropSql;
        await dropCommand.ExecuteNonQueryAsync();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createSql;
        await createCommand.ExecuteNonQueryAsync();

        _log.Info($"Created {tableName} table in {databaseName}.");
    }
}
