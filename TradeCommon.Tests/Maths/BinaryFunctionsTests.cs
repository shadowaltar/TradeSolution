using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Utils.Evaluation;

namespace TradeCommon.Maths.Tests;

[TestFixture]
public class BinaryFunctionsTests
{
    [Test]
    public void BinomialCoefficientTest()
    {
        Assert.That(BinaryFunctions.BinomialCoefficient(10d, 1d), Is.EqualTo(10));
        Assert.That(BinaryFunctions.BinomialCoefficient(10m, 2m), Is.EqualTo(45));
        Assert.That(BinaryFunctions.BinomialCoefficient(10d, 5d), Is.EqualTo(252));
    }
}