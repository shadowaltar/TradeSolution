using log4net.Config;
using OfficeOpenXml;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
