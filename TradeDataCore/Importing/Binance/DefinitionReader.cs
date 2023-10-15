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
    private readonly IStorage _storage;

    public DefinitionReader(IStorage storage)
    {
        _storage = storage;
    }

    public async Task<List<Security>?> ReadAndSave(SecurityType type)
    {
        const int assetPrecision = 8;
        const string exchange = ExternalNames.Binance;
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
            var filterArray = symbolObj["filters"]?.AsArray();
            decimal? lotSize = null;
            decimal? maxLotSize = null;
            decimal? minNotional = null;
            var priceFilterObj = filterArray?.FirstOrDefault(a => a.GetString("filterType") == "PRICE_FILTER")?.AsObject();
            var lotSizeFilterObj = filterArray?.FirstOrDefault(a => a.GetString("filterType") == "LOT_SIZE")?.AsObject();
            var notionalFilterObj = filterArray?.FirstOrDefault(a => a.GetString("filterType") == "NOTIONAL")?.AsObject();
            lotSize = lotSizeFilterObj?.GetDecimal("minQty");
            maxLotSize = lotSizeFilterObj?.GetDecimal("maxQty");
            minNotional = notionalFilterObj?.GetDecimal("minNotional");
            var security = new Security
            {
                Code = code,
                Name = code,
                Exchange = exchange,
                Type = SecurityTypes.Fx,
                SubType = SecurityTypes.Crypto,
                LotSize = lotSize ?? 0,
                TickSize = priceFilterObj.GetDecimal("tickSize"),
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
        var assetNames = securities.Select(s => s.FxInfo!.BaseCurrency).Union(securities.Select(s => s.FxInfo!.QuoteCurrency)).Distinct().OrderBy(s => s);
        foreach (var assetName in assetNames)
        {
            if (assetName.IsBlank()) continue;
            var security = new Security
            {
                Code = assetName,
                Name = assetName,
                Exchange = exchange,
                Type = SecurityTypes.Fx,
                SubType = SecurityTypes.Crypto,
                SecurityType = SecurityType.Fx,
                SecuritySubType = SecurityType.Crypto,
                LotSize = 0, // it is meaningless to set lot size for asset entry
                PricePrecision = assetPrecision,
                QuantityPrecision = assetPrecision,
                Currency = "",
                FxInfo = new FxSecurityInfo
                {
                    BaseCurrency = assetName,
                    QuoteCurrency = "",
                    MaxLotSize = null,
                    MinNotional = null,
                    IsMarginTradingAllowed = false,
                }
            };
            securities.Add(security);
        }

        await _storage.UpsertFxDefinitions(securities);
        return securities;
    }
}