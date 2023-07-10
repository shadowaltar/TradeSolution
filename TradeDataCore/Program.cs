using log4net.Config;
using OfficeOpenXml;
using System.Diagnostics;
using System.Text;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

await Storage.CreateSecurityTable();
await Storage.CreatePriceTable();
var importer = new SecurityDefinitionImporter();
var securities = await importer.DownloadAndParseHongKongSecurityDefinitions();

securities = await Storage.ReadSecurities("HKEX");

var interval = IntervalType.OneDay;
var priceReader = new TradeDataCore.Importing.Yahoo.PriceReader();
var allData = await priceReader.ReadYahooPrices(securities,
    interval, TimeRangeType.TenYears);
