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
using TradeCommon.Database;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();

var buffer = new List<ExtendedOrderBook>();

var log = Logger.New();
var exchange = ExchangeType.Binance;
var symbol = "BTCUSDT";
var environment = EnvironmentType.Prod;
IComponentContext _componentContext;

Register(ExternalNames.Convert(exchange), exchange, environment);

var connectivity = _componentContext.Resolve<IExternalConnectivityManagement>();
connectivity.SetEnvironment(environment);
var service = _componentContext.Resolve<IMarketDataService>();
var storage = _componentContext.Resolve<IStorage>();
var securityService = _componentContext.Resolve<ISecurityService>();
await securityService.Initialize();

var security = securityService.GetSecurity(symbol);
service.NextOrderBook += OnNextOrderBook;

var orderBookTableName = DatabaseNames.GetOrderBookTableName(symbol, ExchangeType.Binance, 5);
var isExists = await storage.IsTableExists(orderBookTableName, DatabaseNames.MarketData);
if (!isExists)
    await storage.CreateOrderBookTable("btcusdt", ExchangeType.Binance, 5);

await service.SubscribeOrderBook(security);

while (true)
    Thread.Sleep(1000);

void OnNextOrderBook(ExtendedOrderBook orderBook)
{
    log.Info(orderBook);
    var clone = orderBook with { };
    clone.Bids.ClearAddRange(orderBook.Bids);
    clone.Asks.ClearAddRange(orderBook.Asks);
    buffer.Add(clone);
    if (buffer.Count >= 10)
    {
        var orderBooks = new List<ExtendedOrderBook>(buffer);
        var task = storage.InsertOrderBooks(orderBooks, orderBookTableName); // fire and forget
    }
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

