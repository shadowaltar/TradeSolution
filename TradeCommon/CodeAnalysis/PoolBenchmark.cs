using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Common;

namespace TradeCommon.CodeAnalysis;

[SimpleJob(RuntimeMoniker.Net70)]
[RPlotExporter]
[MemoryDiagnoser]
public class PoolBenchmark
{
    private Pool<object>? _pool;

    private readonly int _end = 100;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new Pool<object>(0);
    }

    [Benchmark]
    public object?[] Lease()
    {
        var results = new object?[_end];
        for (var i = 0; i < _end; i++)
        {
            results[i] = _pool?.Lease();
        }
        return results;
    }
}
