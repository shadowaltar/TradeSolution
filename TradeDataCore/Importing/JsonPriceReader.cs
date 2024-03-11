using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.Instruments;

namespace TradeDataCore.Importing;
public class JsonPriceReader
{
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;

    public JsonPriceReader(IStorage storage, ISecurityService securityService)
    {
        _storage = storage;
        _securityService = securityService;
    }

    public async Task<Dictionary<(string, string, string), (long, int)>> Import(string json)
    {
        var results = new Dictionary<(string, string, string), (long, int)>();
        var content = await File.ReadAllTextAsync(json);
        var p = Json.Deserialize<List<ExtendedOhlcPrice>>(content);
        var priceGroups = p.GroupBy(p => (p.Code, p.Ex, p.I));
        foreach (var group in priceGroups)
        {
            var (code, exchangeStr, intervalStr) = group.Key;
            var exchange = ExchangeTypeConverter.Parse(exchangeStr);
            var secType = exchange == ExchangeType.Binance ? SecurityType.Fx : SecurityType.Equity;
            var security = await _securityService.GetSecurity(code, exchange, secType);
            if (security == null)
                continue;

            var interval = IntervalTypeConverter.Parse(intervalStr);
            var i = await _storage.InsertPrices(security.Id, interval, secType, group.OfType<OhlcPrice>().ToList());
            results[group.Key] = i;
        }
        return results;
    }
}
