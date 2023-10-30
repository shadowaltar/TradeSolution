using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Providers;
using TradeDataCore.Essentials;

namespace TradeDataCore.Instruments;
public interface ISecurityService : ISecurityDefinitionProvider
{
    bool IsInitialized { get; }
    Task<List<Security>> Initialize();
    Task<List<OhlcPrice>> ReadPrices(int securityId, IntervalType interval, SecurityType securityType, DateTime start, DateTime? end = null, int priceDecimalPoints = 16);
    Task<Dictionary<int, List<ExtendedOhlcPrice>>> ReadAllPrices(List<Security> securities, IntervalType interval, SecurityType securityType, TimeRangeType range);
    Task<(int securityId, int count)> UpsertPrices(int id, IntervalType interval, SecurityType secType, List<OhlcPrice> prices);
    List<Security> GetAssets(ExchangeType exchange = ExchangeType.Unknown);
    Task<List<Security>> GetSecurities(SecurityType securityType, ExchangeType exchange = ExchangeType.Unknown, bool requestExternal = false);
    Task<List<Security>> GetSecurities(List<int> securityIds, bool requestExternal = false);
    Task<Security?> GetSecurity(string code, string exchange, bool requestExternal = false);
    Task<Security?> GetSecurity(string code, ExchangeType exchange, SecurityType securityType = SecurityType.Unknown, bool requestExternal = false);
    Task<Security?> GetSecurity(int securityId, bool requestExternal = false);
    Task<List<ExtendedOrderBook>> GetOrderBookHistory(Security security, int level, DateTime date);
    Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType interval, DateTime end, int lookBackPeriod);
    Task<List<OhlcPrice>> GetOhlcPrices(Security security, IntervalType type, DateTime start, DateTime end);
    Task<Dictionary<int, List<DateTime>>> GetSecurityIdToPriceTimes(Security security, IntervalType interval);
}
