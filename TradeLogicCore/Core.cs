﻿using Common;
using log4net;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Maintenance;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class Core
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<long, IAlgorithmEngine> _engines = new();
    private readonly IServices _services;
    private Reconcilation? _reconciliation;

    public IReadOnlyDictionary<long, IAlgorithmEngine> Engines => _engines;
    public ExchangeType Exchange => Context.Exchange;
    public BrokerType Broker => Context.Broker;
    public EnvironmentType Environment => Context.Environment;
    public Context Context { get; }

    public bool HasRunningAlgorithm => _engines.Count > 0;

    public Core(Context context, IServices services)
    {
        Context = context;
        _services = services;
    }

    /// <summary>
    /// Start a trading algorithm working thread and returns a GUID.
    /// The working thread will not end by itself unless being stopped manually or reaching its designated end time.
    /// 
    /// The following algoParameters need to be provided:
    /// * environment, broker, exchange, user and account details.
    /// * securities to be listened and screened.
    /// * algorithm instance, with position-sizing, entering, exiting, screening, fee-charging logic components.
    /// * when to start: immediately or wait for next start of min/hour/day/week etc.
    /// * when to stop: algorithm halting condition, eg. 2 hours before exchange maintenance.
    /// * what time interval is the algorithm interested in.
    /// Additionally if it is in back-testing mode:
    /// * whether it is a perpetual or ranged testing.
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="engineParameters"></param>
    /// <param name="algoParameters"></param>
    /// <param name="algorithm"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<AlgoSession> Run(EngineParameters engineParameters, AlgorithmParameters algoParameters, Algorithm algorithm)
    {
        _log.Info($"Starting algorithm: {algorithm.GetType().Name}, Id [{algorithm.Id}], VerId [{algorithm.VersionId}]");
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = algoParameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        var (isExternalAvailable, _) = await _services.Admin.Ping();
        if (!isExternalAvailable) throw Exceptions.Unreachable(Context.Broker);
        // some externals need this for calculation like minimal notional amount
        var refPrices = await _services.MarketData.GetPrices(algoParameters.SecurityPool);
        SetMinQuantities(refPrices);

        _reconciliation = new Reconcilation(Context);

        var reconcileStart = startTime.AddDays(-Consts.LookbackDayCount);
        await _reconciliation.RunAll(user, reconcileStart, algoParameters.SecurityPool);

        var engine = new AlgorithmEngine(Context, algorithm, engineParameters);
        _engines[engine.SessionId] = engine;
        var algoSession = engine.AlgoSession;
        _ = Task.Factory.StartNew(async () =>
        {
            Context.Services.Persistence.Insert(algoSession, isSynchronous: true);
            await engine.Run(algoParameters); // this is a blocking call

        }, TaskCreationOptions.LongRunning);

        // the engine execution is a blocking call
        return algoSession;
    }

    /// <summary>
    /// Given min notional and ref price, find securities' min quantity.
    /// </summary>
    /// <param name="refPrices"></param>
    private void SetMinQuantities(Dictionary<string, decimal>? refPrices)
    {
        if (refPrices.IsNullOrEmpty()) return;
        foreach (var (code, price) in refPrices)
        {
            var security = _services.Security.GetSecurity(code);
            if (security == null || price == 0) continue;
            security.MinQuantity = security.MinNotional / price;
        }
    }

    public async Task<ResultCode> StopAlgorithm(long algoSessionId)
    {
        if (_engines.TryGetValue(algoSessionId, out var engine))
        {
            _log.Info("Stopping Algorithm Engine " + algoSessionId);
            await engine.Stop();
            Context.Services.Persistence.Insert(engine.AlgoSession, isSynchronous: true);
            _log.Info("Stopped Algorithm Engine " + algoSessionId);
            _engines.Remove(algoSessionId);
            return ResultCode.StopEngineOk;
        }
        else
        {
            _log.Warn("Failed to stop Algorithm Engine " + algoSessionId);
            return ResultCode.StopEngineFailed;
        }
    }

    public List<AlgoSession> GetActiveAlgoSessions()
    {
        return _engines.Values.Select(e => e.AlgoSession).ToList();
    }

    public async Task<(int expectedCount, int successfulCount)> StopAllAlgorithms()
    {
        var expected = _engines.Keys.Count();
        var successful = 0;
        foreach (var guid in _engines.Keys.ToList())
        {
            if (await StopAlgorithm(guid) == ResultCode.StopEngineOk)
                successful++;
        }
        _log.Info("All Algorithm Engines are stopped.");
        return (expected, successful);
    }
}