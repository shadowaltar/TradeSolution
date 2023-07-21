using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeDataCore;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

Dependencies.Register();