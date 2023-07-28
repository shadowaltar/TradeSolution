using Autofac;
using Common;
using log4net;
using log4net.Config;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore;
using TradeLogicCore.Services;

internal class Program
{
    private static int _maxPrintCount = 10;

    private static Security _security;

    private static async Task Main(string[] args)
    {
        ILog log = Logger.New();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();

        Dependencies.Register(ExternalNames.Binance);

        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        _security = await securityService.GetSecurity("BTCUSDT", ExchangeType.Binance, SecurityType.Fx);

        NewOrderDemo();

        Console.WriteLine("Finished.");
    }

    private async static Task NewSecurityOhlcSubscriptionDemo()
    {
        var printCount = 0;
        var dataService = Dependencies.ComponentContext.Resolve<IRealTimeMarketDataService>();
        dataService.NextOhlc += OnNewOhlc;
        await dataService.SubscribeOhlc(_security);
        while (printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }
        await dataService.UnsubscribeOhlc(_security);

        void OnNewOhlc(int securityId, OhlcPrice price)
        {
            printCount++;
            if (_security.Id != securityId)
            {
                Console.WriteLine("Impossible!");
                return;
            }
            Console.WriteLine(price);
        }
    }

    private static void NewOrderDemo()
    {
        var orderService = Dependencies.ComponentContext.Resolve<IOrderService>();
        var tradeService = Dependencies.ComponentContext.Resolve<ITradeService>();
        orderService.OrderAcknowledged += OnOrderAck;
        tradeService.NextTrades += OnNewTradesReceived;
        var order = new Order
        {
            SecurityId = _security.Id,
            Price = 40000,
            Quantity = 0.0001m,
            Side = Side.Buy,
            Type = OrderType.Limit,
            TimeInForce = OrderTimeInForceType.GoodTillCancel,
        };
        orderService.SendOrder(order);

        static void OnOrderAck(Order order)
        {
            Console.WriteLine(order);
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