using System.Net.Http;
using System.Text.Json.Nodes;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Utils;

namespace TradeDataCore.Importing.Binance
{
    public class DefinitionReader
    {
        public async Task<List<Security>?> ReadAndSave(SecurityType type)
        {
            const string url = "https://data-api.binance.vision/api/v3/exchangeInfo";
            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(url);

            var jo = JsonNode.Parse(json)?.AsObject();
            if (jo == null)
                return null;
            var securities = new List<Security>();
            var symbolsObj = jo["symbols"]!.AsArray();
            foreach (JsonObject symbolObj in symbolsObj)
            {
                if (symbolObj!["status"].ParseString() != "TRADING")
                    continue;

                var security = new Security
                {
                    Code = symbolObj["symbol"].ParseString(),
                    Name = symbolObj["symbol"].ParseString(),
                    Exchange = "BINANCE",
                    Type = "FX",
                    SubType = "CRYPTO",
                    LotSize = 0,
                    Currency = "",
                    FxSetting = new FxSetting
                    {
                        BaseCurrency = symbolObj["baseAsset"].ParseString(),
                        QuoteCurrency = symbolObj["quoteAsset"].ParseString(),
                    }
                };
                securities.Add(security);
            }

            securities = securities.Where(e => SecurityTypeConverter.Matches(e.Type, type)).ToList();
            await Storage.InsertFxDefinitions(securities);
            return securities;
        }
    }
}
