using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Common;
using org.mariuszgromada.math.mxparser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Utils.Evaluation;

namespace TradeCommon.CodeAnalysis;

[SimpleJob(RuntimeMoniker.Net70)]
[RPlotExporter]
[MemoryDiagnoser]
public class BinaryFunctionsBenchmark
{
    private Expression e;

    [GlobalSetup]
    public void Setup()
    {
    }

    [Benchmark]
    public double OrdinaryBinomialCoefficientInDouble()
    {
        return BinaryFunctions.BinomialCoefficient(40d, 19d);
    }

    [Benchmark]
    public double MxBinomialCoefficient()
    {
       var e = new Expression("C(40,19)");
        return e.calculate();
    }
}
