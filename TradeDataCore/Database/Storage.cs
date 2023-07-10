using log4net;
using Microsoft.Data.Sqlite;
using System.Data;
using TradeDataCore.Essentials;
using TradeDataCore.Utils;

namespace TradeDataCore.Database
{
    public class Storage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Storage));

        public static readonly string DatabaseFolder = @"c:\temp";

        public static async Task<int> InsertSecurityFinancialStats(IDictionary<int, Dictionary<FinancialStatType, decimal>> stats)
        {
            var count = 0;
            const string sql =
@$"
INSERT INTO {DatabaseNames.FinancialStatsTable}
    (SecurityId, MarketCap)
VALUES
    ($SecurityId, $MarketCap)
ON CONFLICT (SecurityId)
DO UPDATE SET MarketCap = excluded.MarketCap;
";
            using var connection = await Connect(DatabaseNames.StaticData);
            using var transaction = connection.BeginTransaction();

            SqliteCommand? command = null;
            try
            {
                command = connection.CreateCommand();
                command.CommandText = sql;
                foreach (var (id, map) in stats)
                {
                    if (!map.TryGetValue(FinancialStatType.MarketCap, out var marketCap))
                    {
                        continue;
                    }
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$SecurityId", id);
                    command.Parameters.AddWithValue("$MarketCap", marketCap);
                    count++;
                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
                Log.Info($"Upserted {count} entries into financial stats table.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to upsert into financial stats table.", e);
                transaction.Rollback();
            }
            finally
            {
                command?.Dispose();
            }

            await connection.CloseAsync();
            return count;
        }

        public static async Task InsertStockDefinitions(List<Security> entries)
        {
            const string sql =
@$"
INSERT INTO {DatabaseNames.StockDefinitionTable}
    (Code, Name, Exchange, Type, SubType, LotSize, Currency, Cusip, Isin, YahooTicker, IsShortable, IsEnabled, LocalStartDate, LocalEndDate)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$Currency,$Cusip,$Isin,$YahooTicker,$IsShortable,$IsEnabled,$LocalStartDate,$LocalEndDate)
ON CONFLICT (Code, Exchange)
DO UPDATE SET
    Name = excluded.Name,
    Type = excluded.Type,
    SubType = excluded.SubType,
    LotSize = excluded.LotSize,
    Currency = excluded.Currency,
    Cusip = excluded.Cusip,
    YahooTicker = excluded.YahooTicker,
    Isin = excluded.Isin,
    IsShortable = excluded.IsShortable,
    IsEnabled = excluded.IsEnabled,
    LocalEndDate = excluded.LocalEndDate
;
";

            using var connection = await Connect(DatabaseNames.StaticData);
            using var transaction = connection.BeginTransaction();

            SqliteCommand? command = null;
            try
            {
                command = connection.CreateCommand();
                command.CommandText = sql;

                foreach (var entry in entries)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$Code", entry.Code);
                    command.Parameters.AddWithValue("$Name", entry.Name);
                    command.Parameters.AddWithValue("$Exchange", entry.Exchange);
                    command.Parameters.AddWithValue("$Type", entry.Type.ToUpperInvariant());
                    command.Parameters.AddWithValue("$SubType", entry.SubType?.ToUpperInvariant() ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$LotSize", entry.LotSize);
                    command.Parameters.AddWithValue("$Currency", entry.Currency.ToUpperInvariant());
                    command.Parameters.AddWithValue("$Cusip", entry.Cusip?.ToUpperInvariant() ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$Isin", entry.Isin?.ToUpperInvariant() ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$YahooTicker", entry.YahooTicker?.ToUpperInvariant() ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$IsShortable", entry.IsShortable);
                    command.Parameters.AddWithValue("$IsEnabled", true);
                    command.Parameters.AddWithValue("$LocalStartDate", 0);
                    command.Parameters.AddWithValue("$LocalEndDate", DateTime.MaxValue.ToString("yyyy-MM-dd HH:mm:ss"));

                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
                Log.Info($"Upserted {entries.Count} entries into securities table.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to upsert into securities table.", e);
                transaction.Rollback();
            }
            finally
            {
                command?.Dispose();
            }

            await connection.CloseAsync();
        }

        private static string? GetConnectionString(string databaseName)
        {
            return $"Data Source={Path.Combine(DatabaseFolder, databaseName)}.db";
        }

        public static async Task InsertPrices(int securityId, IntervalType interval, SecurityType secType, List<OhlcPrice> prices)
        {
            var tableName = DatabaseNames.GetPriceTableName(interval, secType);
            string sql =
@$"
INSERT INTO {tableName}
    (SecurityId, Open, High, Low, Close, Volume, StartTime)
VALUES
    ($SecurityId, $Open, $High, $Low, $Close, $Volume, $StartTime)
ON CONFLICT (SecurityId, StartTime)
DO UPDATE SET
    Open = excluded.Open AND
    High = excluded.High AND
    Low = excluded.Low AND
    Close = excluded.Close AND
    Volume = excluded.Volume;
";

            using var connection = await Connect(DatabaseNames.MarketData);
            using var transaction = connection.BeginTransaction();

            SqliteCommand? command = null;
            try
            {
                command = connection.CreateCommand();
                command.CommandText = sql;

                foreach (var price in prices)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$SecurityId", securityId);
                    command.Parameters.AddWithValue("$Open", price.Open);
                    command.Parameters.AddWithValue("$High", price.High);
                    command.Parameters.AddWithValue("$Low", price.Low);
                    command.Parameters.AddWithValue("$Close", price.Close);
                    command.Parameters.AddWithValue("$Volume", price.Volume);
                    command.Parameters.AddWithValue("$StartTime", price.Start);

                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
                Log.Info($"Upserted {prices.Count} prices into prices table.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to upsert into prices table.", e);
                transaction.Rollback();
            }
            finally
            {
                command?.Dispose();
            }

            await connection.CloseAsync();
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
            using var connection = await Connect(DatabaseNames.StaticData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            Log.Info($"Created {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
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

            Log.Info($"Created {DatabaseNames.FxDefinitionTable} table in {DatabaseNames.StaticData}.");
        }

        public static async Task CreatePriceTable(IntervalType interval, SecurityType securityType)
        {
            var tableName = DatabaseNames.GetPriceTableName(interval, securityType);
            string dropSql =
@$"
DROP TABLE IF EXISTS {tableName};
DROP INDEX IF EXISTS idx_sec_start;
DROP INDEX IF EXISTS idx_sec;
";
            string createSql =
@$"
CREATE TABLE IF NOT EXISTS {tableName} (
    SecurityId INT NOT NULL,
    Open REAL NOT NULL,
    High REAL NOT NULL,
    Low REAL NOT NULL,
    Close REAL NOT NULL,
    Volume REAL NOT NULL,
    StartTime INT NOT NULL,
    UNIQUE(SecurityId, StartTime)
);
CREATE UNIQUE INDEX idx_sec_start
ON {tableName} (SecurityId, StartTime);
CREATE INDEX idx_sec
ON {tableName} (SecurityId);
";
            using var connection = await Connect(DatabaseNames.MarketData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            Log.Info($"Created {tableName} table in {DatabaseNames.MarketData}.");
        }

        public static async Task CreateFinancialStatsTable()
        {
            const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.FinancialStatsTable};
DROP INDEX IF EXISTS idx_sec;
";
            const string createSql =
@$"CREATE TABLE IF NOT EXISTS {DatabaseNames.FinancialStatsTable} (
    SecurityId INT NOT NULL,
    MarketCap REAL NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX idx_sec
ON {DatabaseNames.FinancialStatsTable} (SecurityId);
";
            using var connection = await Connect(DatabaseNames.StaticData);

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            Log.Info($"Created {DatabaseNames.FinancialStatsTable} table in {DatabaseNames.StaticData}.");
        }

        public static async Task<Security> ReadSecurity(string exchange, string code, SecurityType type)
        {
            var tableName = DatabaseNames.GetDefinitionTableName(type);
            string sql;
            if (type == SecurityType.Equity)
            {
                sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    Code = $Code AND
    Exchange = $Exchange
";
                if (type == SecurityType.Equity)
                    sql += $" AND Type IN ('{string.Join("','", SecurityTypeConverter.StockTypes)}')";
            }
            else if (type == SecurityType.Fx)
            {
                sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,BaseCurrency,QuoteCurrency,IsShortable
FROM {tableName}
WHERE
    Code = $Code AND
    Exchange = $Exchange
";
            }
            else
            {
                throw new NotImplementedException();
            }

            using var connection = await Connect(DatabaseNames.StaticData);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$Code", code);
            command.Parameters.AddWithValue("$Exchange", exchange);

            using var r = await command.ExecuteReaderAsync();
            var results = new List<Security>();
            while (await r.ReadAsync())
            {
                var security = new Security
                {
                    Id = r["Id"].ToString().ParseInt(),
                    Code = r["Code"].ParseString(),
                    Name = r["Name"].ParseString(),
                    Exchange = r["Exchange"].ParseString(),
                    Type = r["Type"].ParseString(),
                    SubType = r["SubType"].ParseString(),
                    LotSize = r["LotSize"].ToString().ParseInt(),
                    Currency = r["Currency"].ParseString(),
                    Cusip = r["Cusip"].ParseString(null),
                    Isin = r["Isin"].ParseString(null),
                    IsShortable = r["IsShortable"].ToString().ParseBool(),
                };
                var baseCcy = r["BaseCurrency"]?.ParseString();
                var quoteCcy = r["QuoteCurrency"]?.ParseString();
                if (baseCcy != null && quoteCcy!=null)
                {
                    security.FxSetting = new FxSetting
                    {
                        BaseCurrency = baseCcy,
                        QuoteCurrency = quoteCcy,
                    };
                }
                Log.Info($"Read security with code {code} and exchange {exchange} from {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
                return security;
            }
            return null;
        }

        public static async Task<List<Security>> ReadSecurities(string exchange, SecurityType type)
        {
            var tableName = DatabaseNames.GetDefinitionTableName(type);
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string sql;
            if (type == SecurityType.Equity)
            {
                sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,YahooTicker,IsShortable
FROM {tableName}
WHERE
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
";
                if (type == SecurityType.Equity)
                    sql += $" AND Type IN ('{string.Join("','", SecurityTypeConverter.StockTypes)}')";
            }
            else if (type == SecurityType.Fx)
            {
                sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,BaseCurrency,QuoteCurrency,IsShortable
FROM {tableName}
WHERE
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
";
            }
            else
            {
                throw new NotImplementedException();
            }

            using var connection = await Connect(DatabaseNames.StaticData);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$LocalEndDate", now);
            command.Parameters.AddWithValue("$Exchange", exchange);
            command.Parameters.AddWithValue("$Type", type);

            using var r = await command.ExecuteReaderAsync();

            var results = new List<Security>();
            while (await r.ReadAsync())
            {
                var security = new Security
                {
                    Id = r["Id"].ToString().ParseInt(),
                    Code = r["Code"].ParseString(),
                    Name = r["Name"].ParseString(),
                    Exchange = r["Exchange"].ParseString(),
                    Type = r["Type"].ParseString().ToUpperInvariant(),
                    SubType = r["SubType"].ParseString(),
                    LotSize = r["LotSize"].ToString().ParseInt(),
                    Currency = r["Currency"].ParseString(),
                    Cusip = r["Cusip"].ParseString(null),
                    Isin = r["Isin"].ParseString(null),
                    YahooTicker = r["YahooTicker"].ParseString(null),
                    IsShortable = r["IsShortable"].ToString().ParseBool(),
                };
                results.Add(security);
            }
            Log.Info($"Read {results.Count} entries from {DatabaseNames.StockDefinitionTable} table in {DatabaseNames.StaticData}.");
            return results;
        }

        public static async Task<List<FinancialStats>> ReadFinancialStats()
        {
            string sql =
@$"
SELECT SecurityId,MarketCap
FROM {DatabaseNames.FinancialStatsTable}
";
            using var connection = await Connect(DatabaseNames.StaticData);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var r = await command.ExecuteReaderAsync();

            var results = new List<FinancialStats>();
            while (await r.ReadAsync())
            {
                var security = new FinancialStats
                {
                    SecurityId = r["SecurityId"].ToString().ParseInt(),
                    MarketCap = r.GetDecimal("MarketCap"),
                };
                results.Add(security);
            }
            Log.Info($"Read {results.Count} entries from {DatabaseNames.FinancialStatsTable} table in {DatabaseNames.StaticData}.");
            return results;
        }

        public static async Task<List<OhlcPrice>> ReadPrices(int securityId, string intervalStr, DateTime start, DateTime? end = null)
        {
            string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, Volume, StartTime, Interval
FROM {DatabaseNames.StockPrice1hTable}
WHERE
    Interval = $Interval AND
    SecurityId = $SecurityId AND
    StartTime > $StartTime
";
            if (end != null)
                sql += $" AND StartTime <= $EndTime";

            using var connection = await Connect(DatabaseNames.MarketData);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$Interval", intervalStr);
            command.Parameters.AddWithValue("$SecurityId", securityId);
            command.Parameters.AddWithValue("$StartTime", start);
            command.Parameters.AddWithValue("$EndTime", end);

            using var r = await command.ExecuteReaderAsync();

            var results = new List<OhlcPrice>();
            while (await r.ReadAsync())
            {
                var price = new OhlcPrice
                (
                    Open: r["Open"].ToString().ParseDecimal(),
                    High: r["High"].ToString().ParseDecimal(),
                    Low: r["Low"].ToString().ParseDecimal(),
                    Close: r["Close"].ToString().ParseDecimal(),
                    Volume: r["Volume"].ToString().ParseDecimal(),
                    Start: r["StartTime"].ToString().ParseDate("yyyy-MM-dd HH:mm:ss")
                );
                results.Add(price);
            }
            Log.Info($"Read {results.Count} entries from {DatabaseNames.StockPrice1hTable} table in {DatabaseNames.MarketData}.");
            return results;
        }

        public static async Task<DataTable> Execute(string sql, string database)
        {
            var entries = new DataTable();

            using var connection = await Connect(database);
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var r = await command.ExecuteReaderAsync();

            if (!r.HasRows)
                return entries;

            for (int i = 0; i < r.FieldCount; i++)
            {
                entries.Columns.Add(new DataColumn(r.GetName(i)));
            }

            int j = 0;
            while (r.Read())
            {
                DataRow row = entries.NewRow();
                entries.Rows.Add(row);

                for (int i = 0; i < r.FieldCount; i++)
                    entries.Rows[j][i] = r.GetValue(i);

                j++;
            }

            Log.Info($"Read {entries.Rows.Count} entries in {database}. SQL: {sql}");
            return entries;
        }

        public static async Task<bool> CheckTableExists(string tableName, string database)
        {
            if (tableName.IsBlank()) return false;
            if (database.IsBlank()) return false;
            const string sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name=@Name;";
            using var conn = await Connect(database);
            using var command = conn.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@Name", tableName);
            object? r = await command.ExecuteScalarAsync();
            return r != null;
        }

        public static void Purge()
        {
            try
            {
                File.Delete(DatabaseNames.MarketData + ".db");
                Log.Info($"Deleted database file {DatabaseNames.MarketData}.db.");
                File.Delete(DatabaseNames.StaticData + ".db");
                Log.Info($"Deleted database file {DatabaseNames.StaticData}.db.");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to purge Sqlite database files.", e);
                throw;
            }
        }

        private static async Task<SqliteConnection> Connect(string database)
        {
            var conn = new SqliteConnection(GetConnectionString(database));
            await conn.OpenAsync();
            return conn;
        }

        public static async Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, TimeRangeType range)
        {
            var now = DateTime.Today;
            var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
            var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(now);
            string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, Volume, StartTime
FROM {DatabaseNames.StockPrice1hTable}
WHERE
    StartTime > $StartTime AND
    SecurityId IN 
";
            var ids = securities.Select((s, i) => (id: s.Id, param: $"$Id{i}")).ToArray();
            var securityMap = securities.ToDictionary(s => s.Id, s => s);
            sql = sql + "(" + string.Join(",", ids.Select(p => p.param)) + ")";
            using var connection = await Connect(DatabaseNames.MarketData);

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$StartTime", start);

            for (int i = 0; i < ids.Length; i++)
            {
                command.Parameters.AddWithValue(ids[i].param, ids[i].id);
            }
            using var r = await command.ExecuteReaderAsync();

            var results = new Dictionary<int, List<ExtendedOhlcPrice>>();
            while (await r.ReadAsync())
            {
                var secId = r.GetInt32("SecurityId");
                if (!securityMap.TryGetValue(secId, out var sec))
                    continue;
                var list = results.GetOrCreate(secId);
                var price = new ExtendedOhlcPrice
                (
                    Code: sec.Code,
                    Exchange: sec.Exchange,
                    Open: r.GetDecimal("Open"),
                    High: r.GetDecimal("High"),
                    Low: r.GetDecimal("Low"),
                    Close: r.GetDecimal("Close"),
                    Volume: r.GetDecimal("Volume"),
                    Interval: intervalStr,
                    Start: r.GetString("StartTime").ParseDate("yyyy-MM-dd HH:mm:ss")
                );
                list.Add(price);
            }
            Log.Info($"Read {results.Count} entries from {DatabaseNames.StockPrice1hTable} table in {DatabaseNames.MarketData}.");
            return results;
        }
    }
}
