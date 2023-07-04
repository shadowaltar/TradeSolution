using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeDataCore;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


var importer = new SecurityDefinitionImporter();
await importer.DownloadAndParseHongKongSecurityDefinitions();
//Storage.SaveStaticData<Security>("Securities", securities);