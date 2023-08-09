using Common;
using log4net;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Externals;

namespace TradeConnectivity.Binance.Services;

public class Reference : IExternalReferenceManagement
{
    private static readonly ILog _log = Logger.New();

    public async Task<List<Security>> ReadAndPersistSecurities(SecurityType type)
    {
        const string url = "https://data-api.binance.vision/api/v3/exchangeInfo";

        var jo = await HttpHelper.ReadJson(url, _log);
        if (jo == null)
            return new();
        var securities = new List<Security>();
        var assetNames = new HashSet<string>();
        var symbolsObj = jo["symbols"]!.AsArray();
        foreach (JsonObject symbolObj in symbolsObj.Cast<JsonObject>())
        {
            if (symbolObj!["status"].ParseString() != "TRADING")
                continue;

            var baseAsset = symbolObj["baseAsset"].ParseString();
            var quoteAsset = symbolObj["quoteAsset"].ParseString();
            var symbol = symbolObj["symbol"].ParseString();
            var security = new Security
            {
                Code = symbol,
                Name = symbol,
                Exchange = ExternalNames.Binance.ToUpperInvariant(),
                Type = SecurityTypes.Fx,
                SubType = SecurityTypes.Crypto,
                LotSize = 0,
                Currency = "",
                FxInfo = new FxSecurityInfo
                {
                    BaseCurrency = baseAsset,
                    QuoteCurrency = quoteAsset,
                }
            };
            securities.Add(security);

            if (assetNames.Add(baseAsset))
            {
                var asset = CreateAsset(baseAsset);
                securities.Add(asset);
            }

            if (assetNames.Add(quoteAsset))
            {
                var asset = CreateAsset(quoteAsset);
                securities.Add(asset);
            }
        }

        securities = securities.Where(e => SecurityTypeConverter.Matches(e.Type, type)).ToList();
        await Storage.InsertFxDefinitions(securities);
        return securities;
    }

    private static Security CreateAsset(string assetName)
    {
        return new Security
        {
            Code = assetName,
            Name = assetName,
            Exchange = ExternalNames.Binance.ToUpperInvariant(),
            Type = SecurityTypes.Fx,
            SubType = SecurityTypes.Crypto,
            LotSize = 0,
            Currency = "",
            FxInfo = new FxSecurityInfo
            {
                BaseCurrency = assetName,
                QuoteCurrency = "",
            }
        };
    }
}
