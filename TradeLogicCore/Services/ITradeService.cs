using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface ITradeService
{
    event Action<Trade> NewTrade;

    IReadOnlyDictionary<int, int> TradeToOrderIds { get; }
}