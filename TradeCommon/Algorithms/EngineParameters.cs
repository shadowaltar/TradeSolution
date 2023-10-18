namespace TradeCommon.Algorithms;

public record EngineParameters(bool CancelOpenOrdersOnStart = true,
                               bool CloseOpenPositionsOnStop = true,
                               bool CloseOpenPositionsOnStart = true);
