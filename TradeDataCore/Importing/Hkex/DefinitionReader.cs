using log4net;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;
using TradeDataCore.Utils;

namespace TradeDataCore.Importing.Hkex
{
    public class DefinitionReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefinitionReader));

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
}
