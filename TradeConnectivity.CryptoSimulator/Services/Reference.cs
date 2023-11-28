using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Externals;

namespace TradeConnectivity.CryptoSimulator.Services;

[Obsolete]
public class Reference : IExternalReferenceManagement
{
    private static readonly ILog _log = Logger.New();

    //public async Task<List<Security>> ReadAndPersistSecurities(SecurityType type)
    //{
    //    var btc = new Security
    //    {
    //        Id = 2,
    //        Code = "BTC",
    //        Name = "BTC",
    //        Exchange = ExternalNames.Simulator.ToUpperInvariant(),
    //        Type = SecurityTypes.Fx,
    //        SecurityType = SecurityType.Fx,
    //        SubType = SecurityTypes.Crypto,
    //        LotSize = 0,
    //        Currency = "",
    //        FxInfo = new FxSecurityInfo
    //        {
    //            BaseCurrency = "BTC",
    //            QuoteCurrency = "",
    //            IsMarginTradingAllowed = true,
    //            MaxLotSize = 1000,                
    //        },
    //        TickSize = 0,            
    //        ExchangeType = ExchangeType.Simulator,
    //        MinNotional = 0,
    //        MinQuantity = 0.0001m,
    //        PricePrecision = 8,
    //        QuantityPrecision = 8,
    //    };
    //    var usdt = new Security
    //    {
    //        Id = 3,
    //        Code = "USDT",
    //        Name = "USDT",
    //        Exchange = ExternalNames.Simulator.ToUpperInvariant(),
    //        Type = SecurityTypes.Fx,
    //        SecurityType = SecurityType.Fx,
    //        SubType = SecurityTypes.Crypto,
    //        LotSize = 0,
    //        Currency = "",
    //        FxInfo = new FxSecurityInfo
    //        {
    //            BaseCurrency = "USDT",
    //            QuoteCurrency = "",
    //            IsMarginTradingAllowed = true,
    //            MaxLotSize = 1000,                
    //        },
    //        TickSize = 0,            
    //        ExchangeType = ExchangeType.Simulator,
    //        MinNotional = 0,
    //        MinQuantity = 0.0001m,
    //        PricePrecision = 8,
    //        QuantityPrecision = 8,
    //    };
    //    var btcusdt = new Security
    //    {
    //        Id = 1,
    //        Code = "BTCUSDT",
    //        Name = "BTCUSDT",
    //        Type = SecurityTypes.Fx,
    //        Exchange = ExternalNames.Simulator.ToUpperInvariant(),
    //        ExchangeType = ExchangeType.Simulator,
    //        SubType = SecurityTypes.Crypto,
    //        LotSize = 0.00005m,
    //        TickSize = 0.01m,
    //        Currency = "USDT",
    //        FxInfo = new FxSecurityInfo
    //        {
    //            BaseCurrency = "BTC",
    //            QuoteCurrency = "USDT",
    //            BaseAsset = btc,
    //            QuoteAsset = usdt,
    //            MaxLotSize = 9000,
    //        },
    //        QuoteSecurity = usdt,
    //        MinNotional = 5m,
    //        PricePrecision = 8,
    //        QuantityPrecision = 8,
    //    };

    //    await Storage.UpsertFxDefinitions(new List<Security> { btcusdt, btc, usdt });
    //    return securities;
    //}

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
