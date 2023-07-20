using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.Essentials;
using TradeDataCore.Importing.Binance;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var r = new HistoricalPriceReader();
await r.ReadPrices("", DateTime.Now, DateTime.Now, IntervalType.OneDay);