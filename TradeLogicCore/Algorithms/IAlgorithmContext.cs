using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmContext<T>
{
    IServices Services { get; }

    List<Security> SecurityPool { get; }
    
    bool IsBackTesting { get; }
}