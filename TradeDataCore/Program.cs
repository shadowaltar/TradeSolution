using log4net.Config;
using OfficeOpenXml;
using System.Data;
using System.Text;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Exporting;
using TradeDataCore.Importing.Binance;
using TradeDataCore.Utils;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var r = new HistoricalPriceReader();
await r.ReadPrices("", DateTime.Now, DateTime.Now, IntervalType.OneDay);