using Common;
using log4net;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;

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
            var code = symbolObj.GetString("symbol");
            var exchange = ExternalNames.Binance.ToUpperInvariant();
            var filterArray = symbolObj["filters"]?.AsArray();
            double? lotSize = null;
            double? maxLotSize = null;
            double? minNotional = null;
            var lotSizeFilterObj = filterArray?.FirstOrDefault(a => a.GetString("filterType") == "LOT_SIZE")?.AsObject();
            var notionalFilterObj = filterArray?.FirstOrDefault(a => a.GetString("filterType") == "NOTIONAL")?.AsObject();
            lotSize = lotSizeFilterObj?.GetDouble("minQty");
            maxLotSize = lotSizeFilterObj?.GetDouble("maxQty");
            minNotional = notionalFilterObj?.GetDouble("minNotional");
            var security = new Security
            {
                Code = code,
                Name = code,
                Exchange = exchange,
                Type = SecurityTypes.Fx,
                SubType = SecurityTypes.Crypto,
                LotSize = lotSize ?? 0,
                PricePrecision = symbolObj.GetInt("quotePrecision"),
                QuantityPrecision = symbolObj.GetInt("baseAssetPrecision"),
                Currency = "",
                FxInfo = new FxSecurityInfo
                {
                    BaseCurrency = symbolObj.GetString("baseAsset").ToUpperInvariant(),
                    QuoteCurrency = symbolObj.GetString("quoteAsset").ToUpperInvariant(),
                    MaxLotSize = maxLotSize,
                    MinNotional = minNotional,
                    IsMarginTradingAllowed = symbolObj.GetBoolean("isMarginTradingAllowed"),
                }
            };
            securities.Add(security);
        }

        // find assets
        var assetNames = securities.Select(s => s.FxInfo!.BaseCurrency).Union(securities.Select(s => s.FxInfo!.QuoteCurrency)).Distinct().ToList();

        await Storage.UpsertFxDefinitions(securities);
        return securities;
    }
}