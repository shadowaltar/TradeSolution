using NUnit.Framework;
using TradeCommon.Calculations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Calculations.Tests;

[TestFixture()]
public class ExponentialMovingAverageTests
{
    [Test()]
    public void DecimalTest()
    {
        var indicator = new ExponentialMovingAverage(5, 2);

        var initialDecimalValues = new List<decimal> { 500, 521.530235175848m, 476.508433790438m, 460.96972438806m, 456.117839984465m, 422.351739200615m, 385.584186462771m, 386.700246596633m, 412.156943899356m, 447.453216155654m, };
        var expectedDecimalResults = new List<decimal> { decimal.MinValue, decimal.MinValue, decimal.MinValue, decimal.MinValue, 483.0252466677622m, 462.80074417871313333333333333m, 437.06189160673242222222222223m, 420.27467660336594814814814815m, 417.56876570202929876543209877m, 427.53024918657086584362139918m, };

        var actualResults = new List<decimal>();
        foreach (var value in initialDecimalValues)
        {
            var actual = indicator.Next(value);
            Console.WriteLine(actual);
            actualResults.Add(actual);
        }

        for (var i = 0; i < expectedDecimalResults.Count; i++)
        {
            Assert.That(actualResults[i], Is.EqualTo(expectedDecimalResults[i]));
        }
    }
    [Test()]
    public void DoubleTest()
    {
        var indicator = new ExponentialMovingAverage(10, 2);

        var initialValues = new List<double> { 22.2734, 22.194, 22.0847, 22.1741, 22.184, 22.1344, 22.2337, 22.4323, 22.2436, 22.2933, 22.1542, 22.3926, 22.3816, 22.6109, 23.3558, 24.0519, 23.753, 23.8324, 23.9516, 23.6338, 23.8225, 23.8722, 23.6537, 23.187, 23.0976, 23.326, 22.6805, 23.0976, 22.4025, 22.1725, };
        //var expectedDecimalResults = new List<decimal> { decimal.MinValue, decimal.MinValue, decimal.MinValue, decimal.MinValue, 483.0252466677622m, 462.80074417871313333333333333m, 437.06189160673242222222222223m, 420.27467660336594814814814815m, 417.56876570202929876543209877m, 427.53024918657086584362139918m, };

        var actualResults = new List<double>();
        foreach (var value in initialValues)
        {
            var actual = indicator.Next(value);
            Console.WriteLine(actual);
            actualResults.Add(actual);
        }

        //for (var i = 0; i < expectedDecimalResults.Count; i++)
        //{
        //    Assert.That(actualResults[i], Is.EqualTo(expectedDecimalResults[i]));
        //}
    }
}