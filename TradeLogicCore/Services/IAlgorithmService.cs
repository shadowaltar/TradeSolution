using TradeCommon.Algorithms;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
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
    void RecordExecution(AlgoEntry current);
    void CopyOver(AlgoEntry current, AlgoEntry last, decimal price);
    void InitializeSession(EngineParameters engineParameters);
}