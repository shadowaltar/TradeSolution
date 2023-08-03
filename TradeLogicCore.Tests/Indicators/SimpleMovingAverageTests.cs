using NUnit.Framework;
using TradeLogicCore.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators.Tests;

[TestFixture()]
public class SimpleMovingAverageTests
{
    [Test()]
    public void DecimalTest()
    {
        var indicator = new SimpleMovingAverage(5, PriceElementType.Close, calculateFromBeginning: false);

        var initialDecimalValues = new List<decimal> { 500m, 469.498714382133m, 444.305406166675m, 426.158485725809m, 410.631731418528m, 441.994484198811m, 406.212421972098m, 388.491845436069m, 409.004726858233m, 393.290793806733m, 420.366864666843m, 456.008356207671m, 469.753743502962m, 493.657314114552m, 526.698762872971m, 522.983644490729m, 472.798284669417m, 438.549660199927m, 451.998427502432m, 435.202779309358m, 470.20598944202m, 495.023439183816m, 507.906394395597m, 511.569857883535m, 465.280511457291m, 491.056648893068m, 477.961734056596m, 467.351611268847m, 444.482243861798m, 450.851770570474m, };
        var expectedDecimalResults = new List<decimal> { decimal.MinValue, decimal.MinValue, decimal.MinValue, decimal.MinValue, 450.118867538629m, 438.5177643783912m, 425.8605058963842m, 414.697793750263m, 411.2670419767478m, 407.7988544543888m, 403.4733305479952m, 413.4325173951098m, 429.6848970084884m, 446.6154144597522m, 473.2970082729998m, 493.820364237777m, 497.1783499301262m, 490.9375332695192m, 482.6057559470952m, 464.3065592343726m, 453.7510282246308m, 458.1960591275106m, 472.0674059666446m, 483.9816920428652m, 489.9972384724518m, 494.1673703626614m, 490.7550293372174m, 482.6440727118674m, 469.22654990752m, 466.3408017301566m, };

        var actualResults = new List<decimal>();
        foreach (var value in initialDecimalValues)
        {
            var actual = indicator.Next(value);
            Console.WriteLine(actual);
            actualResults.Add(actual);
        }

        for (var i = 0; i < expectedDecimalResults.Count; i++)
        {
            Assert.AreEqual(expectedDecimalResults[i], actualResults[i]);
        }
    }
}