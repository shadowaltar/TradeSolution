using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Importing;
using TradeDataCore.Database;

namespace TradeDataCore.Importing.Hkex;

public class DefinitionReader
{
    private static readonly ILog _log = Logger.New();

    public async Task<List<Security>?> ReadAndSave(SecurityType type)
    {
        var securities = await DownloadAndParseHongKongSecurityDefinitions();
        if (securities == null)
            return null;

        securities = securities.Where(e => SecurityTypeConverter.Matches(e.Type, type)).ToList();
        await Storage.InsertStockDefinitions(securities);
        return securities;
    }

    private async Task<List<Security>> DownloadAndParseHongKongSecurityDefinitions()
    {
        const string url = "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx";
        var filePath = Path.GetTempFileName();
        await HttpHelper.ReadFile(url, filePath);

        var reader = new ExcelReader();
        var securities = reader.ReadSheet<Security>(filePath, "StaticData.HKSecurityExcelDefinition",
            new ExcelImportSetting
            {
                HeaderSkipLineCount = 2,
                HardcodedValues = new()
                {
                    { nameof(Security.Exchange), ExchangeNames.Hkex },
                    { nameof(Security.Currency), ForexNames.Hkd },
                    { nameof(Security.YahooTicker), new ComplexMapping(code => Identifiers.ToYahooSymbol((string)code, ExchangeNames.Hkex), nameof(Security.Code)) },
                }
            });
        if (securities == null)
        {
            _log.Warn($"No securities are downloaded or parsed for HKEX from {url}");
            return new List<Security>();
        }
        securities = securities.Where(s => !s.Code.IsBlank()).ToList();

        _log.Info($"Downloaded and parsed {securities.Count} securities for HKEX from {url}");
        return securities;
    }
}
