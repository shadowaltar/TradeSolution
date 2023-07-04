using log4net;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Importing;

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
                HardcodedValues = new() { { "Exchange", "HKEX" } }
            });
        if (securities == null)
        {
            Log.Warn($"No securities are downloaded or parsed for HKEX from {url}");
            return new List<Security>();
        }


        Log.Info($"Downloaded and parsed {securities.Count} securities for HKEX from {url}");
        await Storage.SaveStaticData("Securities", securities);
        return securities;
    }
}
