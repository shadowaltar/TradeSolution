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
    private readonly ISecurityService _securityService;

    public JsonPriceReader(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<Dictionary<(string, string, string), (int, int)>> Import(string json)
    {
        var results = new Dictionary<(string, string, string), (int, int)>();
        var content = await File.ReadAllTextAsync(json);
        var p = await Json.Deserialize<List<ExtendedOhlcPrice>>(content);
        var priceGroups = p.GroupBy(p => (p.Id, p.Ex, p.I));
        foreach (var group in priceGroups)
        {
            var (code, exchangeStr, intervalStr) = group.Key;
            var exchange = ExchangeTypeConverter.Parse(exchangeStr);
            var secType = exchange == ExchangeType.Binance ? SecurityType.Fx : SecurityType.Equity;
            var security = await _securityService.GetSecurity(code, exchange, secType);
            var interval = IntervalTypeConverter.Parse(intervalStr);
            var i = await Storage.UpsertPrices(security.Id, interval, secType, group.OfType<OhlcPrice>().ToList());
            results[group.Key] = i;
        }
        return results;
    }
}
