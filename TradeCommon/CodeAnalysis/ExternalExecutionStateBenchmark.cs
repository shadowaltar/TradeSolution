﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TradeCommon.Runtime;

namespace TradeCommon.CodeAnalysis;

[SimpleJob(RuntimeMoniker.Net70)]
[RPlotExporter]
[MemoryDiagnoser]
public class ExternalExecutionStateBenchmark
{
    [Benchmark]
    public ExternalQueryState New()
    {
        return new ExternalQueryState
        {
            Action = ActionType.SendOrder,
            ExternalId = 0,
            ResultCode = ResultCode.Unknown,
            Description = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        };
    }
}

