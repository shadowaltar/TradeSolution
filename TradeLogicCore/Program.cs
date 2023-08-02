using Autofac;
using Common;
using log4net;
using log4net.Config;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore.Services;
using Dependencies = TradeLogicCore.Dependencies;

internal class Program
{
    private static int _maxPrintCount = 10;

    private static async Task Main(string[] args)
    {
        ILog log = Logger.New();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();

        Dependencies.Register(ExternalNames.Binance);

        await NewOrderDemo();

        Console.WriteLine("Finished.");
    }

    private async static Task NewSecurityOhlcSubscriptionDemo()
    {
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);

        var printCount = 0;
        var dataService = Dependencies.ComponentContext.Resolve<IRealTimeMarketDataService>();
        dataService.NextOhlc += OnNewOhlc;
        await dataService.SubscribeOhlc(security);
        while (printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }
        await dataService.UnsubscribeOhlc(security);

        void OnNewOhlc(int securityId, OhlcPrice price)
        {
            printCount++;
            if (security.Id != securityId)
            {
                Console.WriteLine("Impossible!");
                return;
            }
            Console.WriteLine(price);
        }
    }

    private static async Task NewOrderDemo()
    {
        var portfolioService = Dependencies.ComponentContext.Resolve<IPortfolioService>();
        var orderService = Dependencies.ComponentContext.Resolve<IOrderService>();
        var tradeService = Dependencies.ComponentContext.Resolve<ITradeService>();
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        orderService.OrderAcknowledged += OnOrderAck;
        tradeService.NextTrades += OnNewTradesReceived;

        portfolioService.SelectUser(new User());

        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);
        var order = new Order
        {
            SecurityCode = "BTCUSDT",
            SecurityId = security.Id,
            Price = 10000,
            Quantity = 0.01m,
            Side = Side.Buy,
            Type = OrderType.Limit,
            TimeInForce = OrderTimeInForceType.GoodTillCancel,
        };
        await Task.Run(async () =>
        {
            await portfolioService.GetAccountByName("whatever");
            orderService.SendOrder(order, false);
        });

        while (true)
        {
            Thread.Sleep(100);
        }

        void OnOrderAck(Order order)
        {
            Console.WriteLine(order);

            orderService.CancelOrder(order.Id);
        }

        static void OnNewTradesReceived(List<Trade> trades)
        {
            foreach (var trade in trades)
            {
                Console.WriteLine(trade);
            }
        }
    }
}