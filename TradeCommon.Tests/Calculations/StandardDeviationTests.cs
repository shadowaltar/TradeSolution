using Common;
using org.mariuszgromada.math.mxparser;

namespace TradeCommon.Calculations.Tests;

[TestFixture()]
public class StandardDeviationTests
{
    [Test()]
    public void DecimalTest()
    {
        var indicator = new StandardDeviation(5);

        var initialDecimalValues = new List<decimal> { 10m, 20m, 40m, 80m, 25m, 50m, 75m, 100m, };
        var expectedDecimalResults = new List<decimal> { decimal.MinValue, decimal.MinValue, decimal.MinValue, decimal.MinValue, 27.3861278752583m, 23.8746727726266m, 23.2916293977042m, 29.0258505474m, };

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

        var expression = new Expression($"std({string.Join(',', initialDecimalValues.Take(5))})");
        double expected = expression.calculate();
        Assert.That(expected, Is.EqualTo(expectedDecimalResults[4].ToDouble()));
    }
}