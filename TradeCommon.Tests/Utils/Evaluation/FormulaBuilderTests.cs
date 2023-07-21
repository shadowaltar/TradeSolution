using NUnit.Framework;
using TradeCommon.Utils.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils.Evaluation.Tests;

[TestFixture()]
public class FormulaBuilderTests
{
    [Test()]
    public void SimpleNumericTest()
    {
        var formula = FormulaBuilder.NewLogic("f(x,y) = x * y").Build();
        var result = formula.EvaluateAsNumber(3, 2);
        Assert.That(result, Is.EqualTo(6));
    }

    [Test()]
    public void NewLogicTest()
    {
        Assert.Fail();
    }

    [Test()]
    public void BuildTest()
    {
        Assert.Fail();
    }

    [Test()]
    public void WithFunctionTest()
    {
        Assert.Fail();
    }
}