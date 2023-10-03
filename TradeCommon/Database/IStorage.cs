using Common.Database;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Misc;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Integrity;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;

namespace TradeCommon.Database;
public interface IStorage
{
    event Action<object, string> Success;
    event Action<object, Exception, string> Failed;

    IDatabaseSchemaHelper SchemaHelper { get; }

    Task CreateAccountTable();
    Task CreateBalanceTable();
    Task CreateFinancialStatsTable();
    Task<string> CreateOpenOrderIdTable();
    Task<List<string>> CreateOrderTable(SecurityType securityType);
    Task<List<string>> CreatePositionTable(SecurityType securityType);
    Task<string> CreatePriceTable(IntervalType interval, SecurityType securityType);
    Task CreateSecurityTable(SecurityType type);
    Task<List<string>> CreateTradeTable(SecurityType securityType);
    Task CreateUserTable();
    Task<(string table, string database)> CreateTable<T>(string? tableNameOverride = null) where T : class;
    Task DeleteOpenOrderId(OpenOrderId openOrderId);
    Task<int> InsertMany<T>(IList<T> entries, bool isUpsert, string? sql = null, string? tableNameOverride = null) where T : class, new();
    Task<int> InsertOne<T>(T entry, bool isUpsert, string? sql = null, string? tableNameOverride = null) where T : class, new();
    Task<int> Insert(PersistenceTask task);
    Task<int> Insert<T>(PersistenceTask task) where T : class, new();
    Task<Account?> ReadAccount(string accountName, EnvironmentType environment);
    Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range);
    Task<List<Asset>> ReadAssets(int accountId);
    Task<List<MissingPriceSituation>> ReadDailyMissingPriceSituations(IntervalType interval, SecurityType securityType);
    Task<List<FinancialStat>> ReadFinancialStats();
    Task<List<FinancialStat>> ReadFinancialStats(int secId);
    Task<List<OpenOrderId>> ReadOpenOrderIds();
    Task<List<Order>> ReadOpenOrders(Security? security = null, SecurityType securityType = SecurityType.Unknown);
    Task<List<Order>> ReadOrders(Security security, DateTime start, DateTime end);
    Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16);
    Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime end, int entryCount, int priceDecimalPoints = 16);
    IAsyncEnumerable<OhlcPrice> ReadPricesAsync(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16);
    Task<List<Security>> ReadSecurities(List<int>? ids = null);
    Task<List<Security>> ReadSecurities(SecurityType type, ExchangeType exchange, List<int>? ids = null);
    Task<Security?> ReadSecurity(ExchangeType exchange, string code, SecurityType type);
    Task<List<Trade>> ReadTrades(Security security, DateTime start, DateTime end);
    Task<List<Trade>> ReadTrades(Security security, long orderId);
    Task<List<Position>> ReadPositions(Account account);
    Task<User?> ReadUser(string userName, string email, EnvironmentType environment);
    Task<List<PositionReconciliationRecord>> ReadPositionReconciliationRecords(List<int> securityIds);
    Task UpsertFxDefinitions(List<Security> entries);
    Task<(int securityId, int count)> UpsertPrices(int securityId, IntervalType interval, SecurityType securityType, List<OhlcPrice> prices);
    Task<int> UpsertSecurityFinancialStats(List<FinancialStat> stats);
    Task UpsertStockDefinitions(List<Security> entries);
    Task<long> GetMax(string fieldName, string tableName, string databaseName);
    Task<bool> CheckTableExists(string tableName, string database);
    Task<DataTable> Query(string sql, string database, params TypeCode[] typeCodes);
    Task<DataTable> Query(string sql, string database);
    Task<int> Run(string sql, string database);
    void PurgeDatabase();
}