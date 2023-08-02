using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Diagnostics;

namespace TradeCommon.CodeAnalysis;

[SimpleJob(RuntimeMoniker.Net70)]
[RPlotExporter]
[MemoryDiagnoser]
public class StopwatchBenchmark
{
    [Benchmark]
    public object New()
    {
        return Stopwatch.StartNew();
    }
}
