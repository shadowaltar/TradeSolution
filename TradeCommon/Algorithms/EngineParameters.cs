namespace TradeCommon.Algorithms;

public record EngineParameters(bool CloseOpenOrdersOnStart = true,
                               bool CloseOpenPositionsOnStop = true,
                               bool CloseOpenPositionsOnStart = true);
