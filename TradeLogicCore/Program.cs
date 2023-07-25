﻿using Autofac;
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
Dependencies.Register();

var engines = Dependencies.Container!.Resolve<QuotationEngines>();
engines.Initialize(ExternalNames.Futu);

var engine = engines.FutuQuotationEngine!;
await engine.InitializeAsync();