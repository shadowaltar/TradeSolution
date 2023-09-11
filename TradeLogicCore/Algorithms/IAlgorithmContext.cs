using TradeCommon.Essentials.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmContext<T>
{
    Context Context { get; }

    IServices Services { get; }

    List<Security> SecurityPool { get; }

    bool IsBackTesting { get; }
}