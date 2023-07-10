using log4net;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Importing;
using TradeDataCore.Utils;

namespace TradeDataCore.StaticData;
public class SecurityDefinitionImporter
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SecurityDefinitionImporter));

    public async Task<List<Security>> DownloadAndParseHongKongSecurityDefinitions()
    {
        const string url = "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx";
        var filePath = Path.GetTempFileName();
        var downloader = new WebDownloader();
        await downloader.Download(url, filePath);

        var reader = new ExcelReader();
        var securities = reader.ReadSheet<Security>(filePath, "StaticData.HKSecurityExcelDefinition",
            new ExcelImportSetting
            {
                HeaderSkipLineCount = 2,
                HardcodedValues = new()
                {
                    { nameof(Security.Exchange), "HKEX" },
                    { nameof(Security.Currency), "HKD" },
                    { nameof(Security.YahooTicker), new ComplexMapping(code => Identifiers.ToYahooSymbol((string)code, "HKEX"), nameof(Security.Code)) },
                }
            });
        if (securities == null)
        {
            Log.Warn($"No securities are downloaded or parsed for HKEX from {url}");
            return new List<Security>();
        }
        securities = securities.Where(s => !s.Code.IsBlank()).ToList();

        Log.Info($"Downloaded and parsed {securities.Count} securities for HKEX from {url}");
        return securities;
    }
}
