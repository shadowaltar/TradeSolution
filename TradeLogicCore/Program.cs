using Autofac;
using Common;
using log4net;
using log4net.Config;
using System.Text;
using TradeCommon.Constants;
using TradeDataCore.Quotation;
using TradeLogicCore;

ILog log = Logger.New();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();

Dependencies.Register(ExternalNames.CryptoSimulator);

var engines = Dependencies.Container!.Resolve<QuotationEngines>();
await engines.Initialize();

var engine = engines.QuotationEngine!;
await engine.InitializeAsync();