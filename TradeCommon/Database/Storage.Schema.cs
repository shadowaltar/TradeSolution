using BenchmarkDotNet.Columns;
using Common;
using Common.Attributes;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TypeConverter = Common.TypeConverter;

namespace TradeCommon.Database;

public partial class Storage
{
    public async Task CreateUserTable()
    {
        var tableName = DatabaseNames.UserTable;
        string dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_name_environment;
DROP INDEX IF EXISTS idx_{tableName}_email_environment;
";
        string createSql =
@$"
CREATE TABLE IF NOT EXISTS {tableName} (
    Id INTEGER PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL,
    Environment VARCHAR(100) NOT NULL,
    EncryptedPassword VARCHAR(512) NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE,
    UNIQUE(Name, Environment)
);
CREATE UNIQUE INDEX idx_{tableName}_name_environment
    ON {tableName} (Name, Environment);
CREATE UNIQUE INDEX idx_{tableName}_email_environment
    ON {tableName} (Email, Environment);
";

        await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.StaticData);
    }

    public async Task<(string table, string database)> CreateTable<T>()
    {
        string dropSql = CreateDropTableAndIndexSql<T>();
        string createSql = CreateCreateTableAndIndexSql<T>();
        var tuple = DatabaseNames.GetTableAndDatabaseName<T>();
        await DropThenCreate(dropSql, createSql, tuple.tableName, tuple.databaseName);
        return tuple;
    }

    public async Task CreateAccountTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.AccountTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.AccountTable}_name_brokerId;
DROP INDEX IF EXISTS idx_{DatabaseNames.AccountTable}_ownerId;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.AccountTable} (
    Id INTEGER PRIMARY KEY,
    OwnerId INTEGER NOT NULL,
    Name VARCHAR(100) NOT NULL,
    BrokerId INTEGER NOT NULL,
    ExternalAccount VARCHAR(100) NOT NULL,
    Type VARCHAR(100),
    SubType VARCHAR(100),
    FeeStructure VARCHAR(100),
    Environment VARCHAR(10) NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE,
    UNIQUE(Name, Environment)
);
CREATE UNIQUE INDEX idx_{DatabaseNames.AccountTable}_name_environment
    ON {DatabaseNames.AccountTable} (Name, Environment);
CREATE UNIQUE INDEX idx_{DatabaseNames.AccountTable}_ownerId
    ON {DatabaseNames.AccountTable} (OwnerId);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.AccountTable, DatabaseNames.StaticData);
    }

    public async Task CreateBalanceTable()
    {
        string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.BalanceTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.BalanceTable}_accountId;
DROP INDEX IF EXISTS idx_{DatabaseNames.BalanceTable}_assetId;
{GetDropTableUniqueIndexStatement<Balance>(DatabaseNames.BalanceTable)}
";
        string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.BalanceTable} (
    Id INTEGER PRIMARY KEY,
    AssetId INTEGER NOT NULL,
    AccountId INTEGER NOT NULL,
    FreeAmount REAL DEFAULT 0 NOT NULL,
    LockedAmount REAL DEFAULT 0 NOT NULL,
    SettlingAmount REAL DEFAULT 0 NOT NULL,
    UpdateTime DATE
    {GetCreateTableUniqueClause<Balance>()}
);
{GetCreateTableUniqueIndexStatement<Balance>(DatabaseNames.BalanceTable)}
CREATE INDEX idx_{DatabaseNames.BalanceTable}_accountId
    ON {DatabaseNames.BalanceTable} (AccountId);
CREATE INDEX idx_{DatabaseNames.BalanceTable}_assetId
    ON {DatabaseNames.BalanceTable} (AssetId);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.BalanceTable, DatabaseNames.StaticData);
    }

    public async Task CreateSecurityTable(SecurityType type)
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

    private async Task CreateStockDefinitionTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.StockDefinitionTable};
DROP INDEX IF EXISTS idx_code_exchange;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.StockDefinitionTable} (
    Id INTEGER PRIMARY KEY,
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

    private async Task CreateFxDefinitionTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.FxDefinitionTable};
DROP INDEX IF EXISTS idx_code_exchange;
";
        const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.FxDefinitionTable} (
    Id INTEGER PRIMARY KEY,
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
    QuantityPrecision DOUBLE,
    UNIQUE(Code, BaseCurrency, QuoteCurrency, Exchange)
);
CREATE UNIQUE INDEX idx_code_exchange
    ON {DatabaseNames.FxDefinitionTable} (Code, Exchange);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.FxDefinitionTable, DatabaseNames.StaticData);
    }

    public async Task<string> CreatePriceTable(IntervalType interval, SecurityType securityType)
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
    SecurityId INTEGER NOT NULL,
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

    public async Task<List<string>> CreateOrderTable(SecurityType securityType)
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
    ParentOrderId INTEGER NOT NULL,
    StopPrice REAL DEFAULT 0 NOT NULL,
    CreateTime DATE NOT NULL,
    UpdateTime DATE NOT NULL,
    ExternalCreateTime DATE DEFAULT 0,
    ExternalUpdateTime DATE DEFAULT 0,
    TimeInForce VARCHAR(10),
    StrategyId INTEGER DEFAULT 0,
    BrokerId INTEGER DEFAULT 0,
    ExchangeId INTEGER DEFAULT 0,
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

    public async Task<List<string>> CreateTradeTable(SecurityType securityType)
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
    OrderId INTEGER NOT NULL,
    ExternalTradeId INTEGER NOT NULL,
    ExternalOrderId INTEGER NOT NULL,
    Time INT NOT NULL,
    Type VARCHAR(40) NOT NULL,
    Price DOUBLE NOT NULL,
    Quantity DOUBLE NOT NULL,
    Side CHAR(1) NOT NULL,
    Fee DOUBLE NOT NULL,
    FeeAssetId INTEGER NOT NULL,
    BrokerId INTEGER DEFAULT 0,
    ExchangeId INTEGER DEFAULT 0,
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

    public async Task<string> CreateOpenOrderIdTable()
    {
        const string tableName = DatabaseNames.OpenOrderIdTable;
        var dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_{tableName}_securityId;
DROP INDEX IF EXISTS idx_{tableName}_securityType;
";
        var createSql =
@$"
CREATE TABLE IF NOT EXISTS {tableName} (
    OrderId INTEGER PRIMARY KEY,
    SecurityId INTEGER NOT NULL,
    SecurityType VARCHAR(100) NOT NULL
    {GetCreateTableUniqueClause<OpenOrderId>()}
);
CREATE UNIQUE INDEX idx_{tableName}_securityId
    ON {tableName} (SecurityId);
CREATE UNIQUE INDEX idx_{tableName}_securityType
    ON {tableName} (SecurityType);
";

        await DropThenCreate(dropSql, createSql, tableName, DatabaseNames.ExecutionData);
        return tableName;
    }

    public async Task<List<string>> CreatePositionTable(SecurityType securityType)
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

    public async Task<long> GetMax(string fieldName, string tableName, string databaseName)
    {
        using var connection = await Connect(databaseName);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT MAX({fieldName}) FROM {tableName}";
        object? r = await command.ExecuteScalarAsync();
        if (r is long maxId)
        {
            return maxId;
        }
        return long.MinValue;
    }

    public async Task CreateFinancialStatsTable()
    {
        const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.FinancialStatsTable};
DROP INDEX IF EXISTS idx_{DatabaseNames.FinancialStatsTable}_securityId;
";
        const string createSql =
@$"CREATE TABLE IF NOT EXISTS {DatabaseNames.FinancialStatsTable} (
    SecurityId INTEGER NOT NULL,
    MarketCap REAL NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX idx_{DatabaseNames.FinancialStatsTable}_securityId
ON {DatabaseNames.FinancialStatsTable} (SecurityId);
";

        await DropThenCreate(dropSql, createSql, DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData);
    }

    private async Task DropThenCreate(string dropSql, string createSql, string tableName, string databaseName)
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

    public string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert)
    {
        var attr = typeof(T).GetCustomAttribute<StorageAttribute>();
        if (attr == null) throw new InvalidOperationException("Must provide table name.");

        var tableName = attr.TableName;
        var properties = ReflectionUtils.GetPropertyToName(typeof(T)).ShallowCopy();
        var uniqueKeyNames = typeof(T).GetCustomAttribute<UniqueAttribute>()!.FieldNames ?? Array.Empty<string>();
        var targetFieldNames = properties.Select(pair => pair.Key).ToList();
        var targetFieldNamePlaceHolders = targetFieldNames.ToDictionary(fn => fn, fn => placeholderPrefix + fn);

        var ignoreFieldNames = ReflectionUtils.GetDatabaseIgnoredPropertyNames<T>();

        // INSERT INTO (...)
        var sb = new StringBuilder()
            .Append("INSERT INTO ").AppendLine(tableName).Append('(');
        foreach (var name in targetFieldNames)
        {
            if (ignoreFieldNames.Contains(name))
                continue;
            sb.Append(name).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        // VALUES (...)
        sb.AppendLine("VALUES").AppendLine().Append('(');
        foreach (var name in targetFieldNames)
        {
            if (ignoreFieldNames.Contains(name))
                continue;
            sb.Append(targetFieldNamePlaceHolders[name]).Append(",");
        }
        sb.RemoveLast();
        sb.Append(')').AppendLine();

        if (isUpsert && !uniqueKeyNames.IsNullOrEmpty())
        {
            // ON CONFLICT (...)
            sb.Append("ON CONFLICT (");
            foreach (var fn in uniqueKeyNames)
            {
                sb.Append(fn).Append(',');
            }
            sb.RemoveLast();
            sb.Append(')').AppendLine();

            // DO UPDATE SET ...
            sb.Append("DO UPDATE SET ");
            foreach (var fn in targetFieldNames)
            {
                if (ignoreFieldNames.Contains(fn))
                    continue;
                if (uniqueKeyNames.Contains(fn))
                    continue;

                sb.Append(fn).Append(" = excluded.").Append(fn).Append(',');
            }
            sb.RemoveLast();
        }
        return sb.ToString();
    }

    public string CreateDropTableAndIndexSql<T>()
    {
        var type = typeof(T);
        var (table, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        var sb = new StringBuilder();
        sb.Append($"DROP TABLE IF EXISTS ").Append(table).AppendLine(";");

        var uniqueAttributes = type.GetCustomAttributes<UniqueAttribute>().ToList();
        var indexAttributes = type.GetCustomAttributes<IndexAttribute>().ToList();

        for (int i = 0; i < uniqueAttributes.Count; i++)
        {
            var attr = uniqueAttributes[i];
            sb.Append($"DROP INDEX IF EXISTS ")
                .Append("UX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine(";");
        }
        for (int i = 0; i < indexAttributes.Count; i++)
        {
            var attr = indexAttributes[i];
            sb.Append($"DROP INDEX IF EXISTS ")
                .Append("IX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine(";");
        }
        return sb.ToString();
    }

    public string CreateCreateTableAndIndexSql<T>()
    {
        var type = typeof(T);
        var (table, _) = DatabaseNames.GetTableAndDatabaseName<T>();
        var properties = ReflectionUtils.GetPropertyToName(type);

        var uniqueAttributes = type.GetCustomAttributes<UniqueAttribute>().ToList();
        var indexAttributes = type.GetCustomAttributes<IndexAttribute>().ToList();
        var primaryUniqueKeys = uniqueAttributes.FirstOrDefault()?.FieldNames;

        // find attributes attached to a record type's constructor parameter
        var recordPropertyAttributes = new Dictionary<string, List<Attribute>>();
        if (type.IsRecord())
        {
            var constructors = type.GetConstructors();
            foreach (var constructor in constructors)
            {
                var ctorParams = constructor.GetParameters();
                foreach (var ctorParam in ctorParams)
                {
                    if (properties.ContainsKey(ctorParam.Name!))
                    {
                        // it should be a property generated by the record constructor
                        recordPropertyAttributes[ctorParam.Name!] = ctorParam.GetCustomAttributes().ToList();
                    }
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE IF NOT EXISTS ")
            .Append(table).AppendLine(" (");

        foreach (var (name, property) in properties)
        {
            var typeString = TypeConverter.ToSqliteType(property.PropertyType);

            var isIgnored = false;
            var isNotNull = false;
            var isPrimary = false;
            object? defaultValue = null;
            int varcharMax = 0;
            var attributes = property.GetCustomAttributes().ToList();
            if (recordPropertyAttributes.TryGetValue(name, out var otherAttributes))
            {
                attributes.AddRange(otherAttributes);
            }
            foreach (var attr in attributes)
            {
                if (attr is RequiredMemberAttribute) // system attribute
                {
                    isNotNull = true;
                }

                if (attr is not IStorageRelatedAttribute)
                    continue;

                if (attr is DatabaseIgnoreAttribute)
                {
                    isIgnored = true;
                    break;
                }

                if (attr is AutoIncrementOnInsertAttribute)
                {
                    isPrimary = true;
                }
                if (attr is DefaultValueAttribute defaultAttr)
                {
                    defaultValue = defaultAttr.Value;
                }
                if (attr is LengthAttribute lengthAttr && lengthAttr.MaxLength > 0)
                {
                    varcharMax = lengthAttr.MaxLength;
                }
                if (attr is AsJsonAttribute)
                {
                    typeString = "TEXT";
                }
            }

            if (isIgnored) continue;

            var propertyGet = property.GetGetMethod();
            if (propertyGet != null)
            {
                var returnParameter = propertyGet.ReturnParameter;
                isNotNull = Attribute.IsDefined(returnParameter, typeof(NotNullAttribute));
            }

            sb.Append(name).Append(' ').Append(typeString);
            if (property.PropertyType == typeof(string) && varcharMax > 0)
                sb.Append('(').Append(varcharMax).Append(')');
            else if (property.PropertyType.IsEnum)
                sb.Append('(').Append(Consts.EnumDatabaseTypeSize).Append(')');

            if (isPrimary)
                sb.Append(" PRIMARY KEY");
            else if (isNotNull)
                sb.Append(" NOT NULL");
            if (defaultValue != null)
            {
                if (property.PropertyType == typeof(string))
                    sb.Append(" DEFAULT '").Append(defaultValue).Append('\'');
                else
                    sb.Append(" DEFAULT ").Append(defaultValue);
            }
            sb.Append(',');
        }
        if (primaryUniqueKeys.IsNullOrEmpty())
            sb.RemoveLast();
        else
            sb.AppendLine().Append("UNIQUE(").AppendJoin(',', primaryUniqueKeys).AppendLine(")");
        sb.AppendLine(");");

        foreach (var attr in uniqueAttributes)
        {
            sb.Append($"CREATE UNIQUE INDEX ")
                .Append("UX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine()
                .Append("ON ").AppendLine(table)
                .Append('(').AppendJoin(',', attr.FieldNames).AppendLine(");");
        }
        foreach (var attr in indexAttributes)
        {
            sb.Append($"CREATE INDEX ")
                .Append("IX_").Append(table).Append('_')
                .AppendJoin('_', attr.FieldNames)
                .AppendLine()
                .Append("ON ").AppendLine(table)
                .Append('(').AppendJoin(',', attr.FieldNames).AppendLine(");");
        }
        return sb.ToString();
    }

    private string GetCreateTableUniqueClause<T>()
    {
        var type = typeof(T);
        var uniqueKeys = type.GetCustomAttribute<UniqueAttribute>()?.FieldNames;
        return uniqueKeys.IsNullOrEmpty() ? "" : $", UNIQUE({string.Join(", ", uniqueKeys)})";
    }

    private string GetCreateTableUniqueIndexStatement<T>(string tableName)
    {
        var type = typeof(T);
        var uniqueKeys = type.GetCustomAttribute<UniqueAttribute>()?.FieldNames;
        if (uniqueKeys.IsNullOrEmpty())
            return "";
        return @$"
CREATE UNIQUE INDEX
    idx_{tableName}_{string.Join("_", uniqueKeys.Select(k => k.FirstCharLowerCase()))}
ON {tableName}
    ({string.Join(", ", uniqueKeys)});";
    }

    private string GetDropTableUniqueIndexStatement<T>(string tableName)
    {
        var type = typeof(T);
        var uniqueKeys = type.GetCustomAttribute<UniqueAttribute>()?.FieldNames;
        if (uniqueKeys.IsNullOrEmpty())
            return "";
        return @$"
DROP INDEX IF EXISTS 
    idx_{tableName}_{string.Join("_", uniqueKeys.Select(k => k.FirstCharLowerCase()))};";
    }
}
