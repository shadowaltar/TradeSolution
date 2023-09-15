using System.Data;
using System.Reflection;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Integrity;
using TradeCommon.Providers;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;

namespace TradeCommon.Database;
public interface IStorage
{
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
    Task DeleteOpenOrderId(OpenOrderId openOrderId);
    Task Insert(IPersistenceTask task, bool isUpsert = true);
    Task<int> InsertAccount(Account account, bool isUpsert);
    Task<int> InsertBalance(Balance balance, bool isUpsert);
    Task<int> InsertOrder(Order order, bool isUpsert = true);
    Task<int> InsertPosition(Position position, bool isUpsert = true);
    Task<int> InsertTrade(Trade trade, bool isUpsert = true);
    Task InsertOpenOrderId(OpenOrderId openOrderId);
    Task<int> InsertUser(User user);
    Task<Account?> ReadAccount(string accountName, EnvironmentType environment);
    Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range);
    Task<List<Balance>> ReadBalances(int accountId);
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
    Task<List<Security>> ReadSecurities(SecurityType type, string? exchange = null, List<int>? ids = null);
    Task<Security?> ReadSecurity(ExchangeType exchange, string code, SecurityType type);
    Task<List<Trade>> ReadTrades(Security security, DateTime start, DateTime end);
    Task<List<Trade>> ReadTrades(Security security, long orderId);
    Task<User?> ReadUser(string userName, string email, EnvironmentType environment);
    Task UpsertFxDefinitions(List<Security> entries);
    Task<(int securityId, int count)> UpsertPrices(int securityId, IntervalType interval, SecurityType securityType, List<OhlcPrice> prices);
    Task<int> UpsertSecurityFinancialStats(List<FinancialStat> stats);
    Task UpsertStockDefinitions(List<Security> entries);

    void Initialize(ISecurityDefinitionProvider securityService);
    Task<long> GetMax(string fieldName, string tableName, string databaseName);
    Task<bool> CheckTableExists(string tableName, string database);
    Task<DataTable> Query(string sql, string database, params TypeCode[] typeCodes);
    Task<DataTable> Query(string sql, string database);
    (string table, string? schema, string database) GetStorageNames<T>();
    void PurgeDatabase();

    string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert);

    string CreateDropTableAndIndexSql<T>();
}