using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Algorithms;

public interface IAlgorithm<T> where T : IAlgorithmVariables
{
    IAlgorithemContext<T> Context { get; set; }

    T CalculateVariables(decimal price, AlgoEntry<T>? last);

    int IsBuySignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return 0; }

    int IsSellCloseSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return 0; }

    int IsSellSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return 0; }

    int IsBuyCoverSignal(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { return 0; }

    void BeforeSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }
    void AfterSignalDetection(AlgoEntry<T> current, AlgoEntry<T> last, OhlcPrice currentPrice, OhlcPrice? lastPrice) { }

    void BeforeBuy(AlgoEntry<T> entry) { }
    void BeforeSellClose(AlgoEntry<T> entry) { }
    void BeforeBuyStopLoss(AlgoEntry<T> entry) { }
    void AfterBuy(AlgoEntry<T> entry) { }
    void AfterSellClose(AlgoEntry<T> entry) { }
    void AfterBuyStopLoss(AlgoEntry<T> entry) { }

    void BeforeSell(AlgoEntry<T> entry) { }
    void BeforeBuyCover(AlgoEntry<T> entry) { }
    void BeforeSellStopLoss(AlgoEntry<T> entry) { }
    void AfterSell(AlgoEntry<T> entry) { }
    void AfterBuyCover(AlgoEntry<T> entry) { }
    void AfterSellStopLoss(AlgoEntry<T> entry) { }
}