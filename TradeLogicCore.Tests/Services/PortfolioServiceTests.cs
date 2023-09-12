using Common;
using Moq;
using NUnit.Framework;
using TradeCommon.Database;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Services.Tests;

[TestFixture()]
public class PortfolioServiceTests
{

    private static readonly int[][] _fakeTradeInputs = new int[][]
    {
        new[] {100,1000, 1},
        new[] {120,  50, 1},
        new[] {140, 550,-1},
        new[] {110, 400,-1},
        new[] {105, 100,-1},
    };

    [Test()]
    public void MergeTest()
    {
        const int orderId = 1;
        var idGen = new IdGenerator("PortfolioServiceTestsIdGen");
        var trades = new List<Trade>();
        var service = new PortfolioService(
            Mock.Of<IExternalExecutionManagement>(),
            Mock.Of<IExternalAccountManagement>(),
            Mock.Of<Context>(),
            Mock.Of<IOrderService>(),
            Mock.Of<ITradeService>(),
            Mock.Of<ISecurityService>(),
            Mock.Of<Persistence>());
        foreach (var input in _fakeTradeInputs)
        {
            var trade = new Trade
            {
                Id = idGen.NewInt,
                OrderId = orderId,
                Price = input[0],
                Quantity = input[1],
                Side = (Side)input[2],
            };
            trades.Add(trade);
        }

        var position = service.Create(trades[0]);
        for (int i = 1; i < trades.Count; i++)
        {
            service.Merge(position, trades[i]);
        }
    }
}