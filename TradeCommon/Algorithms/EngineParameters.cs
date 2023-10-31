namespace TradeCommon.Algorithms;

/// <summary>
/// Algo engine parameters.
/// </summary>
/// <param name="PreferredQuoteCurrencies">A list of asset security code which are the preferred quote currencies in the engine.
/// The first code in the list is used as the preferred quote currency for any auto-closing features. If not available, then the
/// engine will try to use the followed security codes in this list.</param>
/// <param name="GlobalCurrencyFilter">A list of asset security code which will be visible in the engine,
/// for all interested non-cash or cash/fiat currencies.</param>
/// <param name="AssumeNoOpenPositionOnStart">Assume there is no open position during engine startup.</param>
/// <param name="CancelOpenOrdersOnStart">Whether cancel all open orders on engine starts.</param>
/// <param name="CloseOpenPositionsOnStop">Whether close all open positions before engine stops.</param>
/// <param name="CloseOpenPositionsOnStart">Whether close all open positions on engine starts.</param>
/// <param name="CleanUpNonCashOnStart">Whether clean up (sell) all non-cash assets on engine starts.</param>
public record EngineParameters(List<string> PreferredQuoteCurrencies,
                               List<string>? GlobalCurrencyFilter = null,
                               bool AssumeNoOpenPositionOnStart = true,
                               bool CancelOpenOrdersOnStart = true,
                               bool CloseOpenPositionsOnStop = true,
                               bool CloseOpenPositionsOnStart = true,
                               bool CleanUpNonCashOnStart = true);
