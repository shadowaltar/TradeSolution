using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithmContext<T>
{
    Portfolio Portfolio { get; }

    List<Security> SecurityPool { get; }
}