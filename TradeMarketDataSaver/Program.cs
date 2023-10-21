// See https://aka.ms/new-console-template for more information
using log4net.Config;
using System.ComponentModel;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials;
using TradeDataCore;
using TradeCommon.Runtime;
using Autofac;
using TradeDataCore.MarketData;
using TradeDataCore.Instruments;
using TradeCommon.Essentials.Quotes;
using Common;
using TradeCommon.Externals;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();

var log = Logger.New();
var exchange = ExchangeType.Binance;
var symbol = "BTCUSDT";
var environment = EnvironmentType.Prod;
IComponentContext _componentContext;

Register(ExternalNames.Convert(exchange), exchange, environment);

var connectivity = _componentContext.Resolve<IExternalConnectivityManagement>();
connectivity.SetEnvironment(environment);
var service = _componentContext.Resolve<IMarketDataService>();
var securityService = _componentContext.Resolve<ISecurityService>();
await securityService.Initialize();

var security = securityService.GetSecurity(symbol);

service.NextTick += OnNextTick;

await service.SubscribeTick(security);

while (true)
    Thread.Sleep(1000);

void OnNextTick(int securityId, string securityCode, Tick tick)
{
    log.Info(security.FormatTick(tick));
}

void Register(BrokerType broker, ExchangeType exchange, EnvironmentType environment, ContainerBuilder? builder = null)
{
    builder ??= new ContainerBuilder();

    builder.RegisterModule<Dependencies.DependencyModule>();
    switch (broker)
    {
        case BrokerType.Binance:
            builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
            break;
    }
    builder.RegisterType<ApplicationContext>().As<ApplicationContext>().SingleInstance();

    var container = builder.Build();
    _componentContext = container.Resolve<IComponentContext>();

    // setup context
    var context = container.Resolve<ApplicationContext>();
    context.Initialize(container, environment, exchange, broker);
}

