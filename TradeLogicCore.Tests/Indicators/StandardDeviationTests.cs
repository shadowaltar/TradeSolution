using NUnit.Framework;
using TradeLogicCore.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Quotes;
using org.mariuszgromada.math.mxparser;

namespace TradeLogicCore.Indicators.Tests;

[TestFixture()]
public class StandardDeviationTests
{
    [Test()]
    public void CalculateTest()
    {
        var random = new Random(DateTime.Now.Second);

        var prices = new List<OhlcPrice>();
        var closes = new List<int>();
        var count = 30;
        var date = new DateTime(2023, 1, 1);
        for (int i = 0; i < count; i++)
        {
            var close = random.Next(100, 500);
            closes.Add(close);
            prices.Add((1, 1, 1, close, 1, date));
            date = date.AddDays(1);
        }

        var sdFunc = new StandardDeviationEvaluator(count, PriceElementType.Close, false, false);
        var sd = sdFunc.Calculate(prices);

        var exp = new Expression($"std({string.Join(',', closes)})");
        var expected = exp.calculate();
        Assert.AreEqual(expected, sd);
    }
}