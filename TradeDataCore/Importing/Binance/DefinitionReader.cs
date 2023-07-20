using Common;
using log4net;
using System.Text.Json.Nodes;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.Database;

namespace TradeDataCore.Importing.Binance;

public class DefinitionReader
{
    private static readonly ILog _log = Logger.New();

    public async Task<List<Security>?> ReadAndSave(SecurityType type)
    {
        const string url = "https://data-api.binance.vision/api/v3/exchangeInfo";

        var jo = await HttpHelper.ReadJson(url, _log);
        if (jo == null)
            return null;
        var securities = new List<Security>();
        var symbolsObj = jo["symbols"]!.AsArray();
        foreach (JsonObject symbolObj in symbolsObj.Cast<JsonObject>())
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
                FxInfo = new FxSecurityInfo
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