using TradeDataCore.Essentials;
using TradeDataCore.Importing;

namespace TradeDataCore.StaticData;
public class SecurityDefinitionImporter
{
    public async Task DownloadAndParseHongKongSecurityDefinitions()
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
    }
}
