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

        public static async Task InsertSecurities(List<Security> entries)
        {
            const string sql =
@$"
INSERT INTO {DatabaseNames.SecurityTable}
    (Code, Name, Exchange, Type, SubType, LotSize, Currency, Cusip, Isin, IsShortable, IsEnabled, LocalStartDate, LocalEndDate)
VALUES
    ($Code,$Name,$Exchange,$Type,$SubType,$LotSize,$Currency,$Cusip,$Isin,$IsShortable,$IsEnabled,$LocalStartDate,$LocalEndDate)
ON CONFLICT (Code, Exchange)
DO UPDATE SET LocalEndDate = excluded.LocalEndDate;
";

            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.StaticData));
            await connection.OpenAsync();
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
                    command.Parameters.AddWithValue("$Type", entry.Type);
                    command.Parameters.AddWithValue("$SubType", entry.SubType ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$LotSize", entry.LotSize);
                    command.Parameters.AddWithValue("$Currency", entry.Currency);
                    command.Parameters.AddWithValue("$Cusip", entry.Cusip ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$Isin", entry.Isin ?? (object)DBNull.Value);
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

        public static async Task InsertPrices(int securityId, string intervalStr, List<OhlcPrice> prices)
        {
            const string sql =
@$"
INSERT INTO {DatabaseNames.PriceTable}
    (SecurityId, Open, High, Low, Close, Volume, StartTime, Interval)
VALUES
    ($SecurityId, $Open, $High, $Low, $Close, $Volume, $StartTime, $Interval)
ON CONFLICT (SecurityId, StartTime, Interval)
DO UPDATE SET
    Open = excluded.Open AND
    High = excluded.High AND
    Low = excluded.Low AND
    Close = excluded.Close AND
    Volume = excluded.Volume;
";

            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.MarketData));
            await connection.OpenAsync();
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
                    command.Parameters.AddWithValue("$Interval", intervalStr);

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

        public static async Task CreateSecurityTable()
        {
            const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.SecurityTable};
DROP INDEX IF EXISTS idx_code_exchange;
";
            const string createSql =
@$"
CREATE TABLE IF NOT EXISTS {DatabaseNames.SecurityTable} (
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
    IsShortable BOOLEAN DEFAULT FALSE,
    IsEnabled BOOLEAN DEFAULT TRUE,
    LocalStartDate DATE NOT NULL DEFAULT 0, 
    LocalEndDate DATE NOT NULL
);
CREATE UNIQUE INDEX idx_code_exchange
    ON {DatabaseNames.SecurityTable} (Code, Exchange);
";
            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.StaticData));
            await connection.OpenAsync();

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            Log.Info($"Created {DatabaseNames.SecurityTable} table in {DatabaseNames.StaticData}.");
        }

        public static async Task CreatePriceTable()
        {
            const string dropSql =
@$"
DROP TABLE IF EXISTS {DatabaseNames.PriceTable};
DROP INDEX IF EXISTS idx_sec_start_interval;
";
            const string createSql =
@$"CREATE TABLE IF NOT EXISTS {DatabaseNames.PriceTable} (
    SecurityId INT NOT NULL,
    Open REAL NOT NULL,
    High REAL NOT NULL,
    Low REAL NOT NULL,
    Close REAL NOT NULL,
    Volume REAL NOT NULL,
    StartTime INT NOT NULL,
    Interval VARCHAR(5) NOT NULL
);
CREATE UNIQUE INDEX idx_sec_start_interval
ON {DatabaseNames.PriceTable} (SecurityId, StartTime, Interval);
";
            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.MarketData));
            await connection.OpenAsync();

            using var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = dropSql;
            await dropCommand.ExecuteNonQueryAsync();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createSql;
            await createCommand.ExecuteNonQueryAsync();

            Log.Info($"Created {DatabaseNames.PriceTable} table in {DatabaseNames.MarketData}.");
        }

        public static async Task<Security> ReadSecurity(string exchange, string code)
        {
            string sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,IsShortable
FROM {DatabaseNames.SecurityTable}
WHERE
    Code = $Code AND
    Exchange = $Exchange
";
            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.StaticData));
            await connection.OpenAsync();

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
                Log.Info($"Read security with code {code} and exchange {exchange} from {DatabaseNames.SecurityTable} table in {DatabaseNames.StaticData}.");
                return security;
            }
            return null;
        }

        public static async Task<List<Security>> ReadSecurities(string exchange, string? type = "")
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string sql =
@$"
SELECT Id,Code,Name,Exchange,Type,SubType,LotSize,Currency,Cusip,Isin,IsShortable
FROM {DatabaseNames.SecurityTable}
WHERE
    IsEnabled = true AND
    LocalEndDate > $LocalEndDate AND
    Exchange = $Exchange
";
            if (!type.IsBlank())
                sql += $" AND Type = $Type";

            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.StaticData));
            await connection.OpenAsync();

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
                    Type = r["Type"].ParseString(),
                    SubType = r["SubType"].ParseString(),
                    LotSize = r["LotSize"].ToString().ParseInt(),
                    Currency = r["Currency"].ParseString(),
                    Cusip = r["Cusip"].ParseString(null),
                    Isin = r["Isin"].ParseString(null),
                    IsShortable = r["IsShortable"].ToString().ParseBool(),
                };
                results.Add(security);
            }
            Log.Info($"Read {results.Count} entries from {DatabaseNames.SecurityTable} table in {DatabaseNames.StaticData}.");
            return results;
        }

        public static async Task<List<OhlcPrice>> ReadPrices(int securityId, string intervalStr, DateTime start, DateTime? end = null)
        {
            string sql =
@$"
SELECT SecurityId, Open, High, Low, Close, Volume, StartTime, Interval
FROM {DatabaseNames.PriceTable}
WHERE
    Interval = $Interval AND
    SecurityId = $SecurityId AND
    StartTime > $StartTime
";
            if (end != null)
                sql += $" AND StartTime <= $EndTime";

            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.MarketData));
            await connection.OpenAsync();

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
            Log.Info($"Read {results.Count} entries from {DatabaseNames.PriceTable} table in {DatabaseNames.MarketData}.");
            return results;
        }

        public static async Task<DataTable> Execute(string sql, string database)
        {
            var entries = new DataTable();

            using var connection = new SqliteConnection(GetConnectionString(DatabaseNames.MarketData));
            await connection.OpenAsync();
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
    }
}
