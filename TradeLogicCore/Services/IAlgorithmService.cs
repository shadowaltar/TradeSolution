﻿using TradeCommon.Algorithms;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;

public interface IAlgorithmService
{
    AlgoEntry? GetCurrentEntry(int securityId);

    AlgoEntry? GetLastEntry(int securityId);

    OhlcPrice? GetLastOhlcPrice(int securityId, IntervalType interval);

    AlgoEntry? GetLastEntryAt(int securityId, int offset);

    List<AlgoEntry> GetAllEntries(int securityId);

    List<AlgoEntry> GetExecutionEntries(int securityId);

    List<int> GetAllSecurityIds();

    AlgoSession Session { get; set; }

    /// <summary>
    /// Move the algo entry forward when a new OHLC price signal is received.
    /// </summary>
    void MoveNext(AlgoEntry current, OhlcPrice ohlcPrice);

    AlgoEntry CreateCurrentEntry(Algorithm algorithm, Security security, OhlcPrice ohlcPrice, out AlgoEntry? last);
    
    void CopyOver(AlgoEntry current, AlgoEntry last, decimal price);

    void InitializeSession(EngineParameters engineParameters);

    /// <summary>
    /// Get the cash position from current algo entry.
    /// For FX, it is for the quote currency asset.
    /// For stock, it is usually USD.
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    Asset? GetCash(AlgoEntry current);

    /// <summary>
    /// Get the asset position from algo entry.
    /// For FX, it is for the base currency asset.
    /// For stock, it is the security itself.
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    Asset? GetAsset(AlgoEntry current);
}