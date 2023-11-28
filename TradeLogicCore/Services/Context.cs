using Autofac;
using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;
public class Context : ApplicationContext
{
    private User? _user;
    private Account? _account;
    private Core? _core;
    private IServices? _services;
    private Algorithm? _algorithm;
    private IAlgorithmEngine? _algorithmEngine;

    public Context(IComponentContext container, IStorage storage) : base(container, storage)
    {
    }

    public bool IsBackTesting => _algorithmEngine?.AlgoParameters == null
                ? throw Exceptions.InvalidAlgorithmEngineState()
                : _algorithmEngine.AlgoParameters.IsBackTesting;

    public IServices Services => _services ??= _container?.Resolve<IServices>() ?? throw Exceptions.ContextNotInitialized();

    public Core Core => _core ??= _container?.Resolve<Core>() ?? throw Exceptions.ContextNotInitialized();

    /// <summary>
    /// Get the current user (assigned after login).
    /// </summary>
    public User? User
    {
        get => !IsInitialized ? throw Exceptions.MustLogin() : _user;
        set
        {
            if (value == null)
                throw Exceptions.Invalid<User>("User missing or invalid.");
            _user = value;
            UserId = value.Id;
        }
    }

    /// <summary>
    /// Get the current account (assigned after login).
    /// </summary>
    public Account? Account
    {
        get => !IsInitialized ? throw Exceptions.MustLogin() : _account;
        set
        {
            if (value == null)
                throw Exceptions.InvalidAccount();
            _account = value;
            AccountId = value.Id;
        }
    }

    public void InitializeAlgorithmContext(IAlgorithmEngine algorithmEngine, Algorithm algorithm)
    {
        _algorithmEngine = algorithmEngine;
        _algorithm = algorithm;
    }

    public void Reset()
    {
        _user = null;
        _account = null;
        UserId = 0;
        AccountId = 0;
        _preferredQuoteCurrencies.Clear();
        _currencyWhitelist.Clear();
        _environment = EnvironmentType.Unknown;
        _exchange = ExchangeType.Unknown;
        _exchangeId = 0;
        _broker = BrokerType.Unknown;
        _brokerId = 0;

        ExternalQueryStates.Exchange = Exchange;
        ExternalQueryStates.Environment = Environment;
        ExternalQueryStates.EnvironmentId = ExchangeId;
        ExternalQueryStates.Broker = Broker;
        ExternalQueryStates.BrokerId = BrokerId;
        ExternalConnectionStates.Exchange = Exchange;
        ExternalConnectionStates.Environment = Environment;
        ExternalConnectionStates.EnvironmentId = ExchangeId;
        ExternalConnectionStates.Broker = Broker;
        ExternalConnectionStates.BrokerId = BrokerId;

        IsInitialized = false;
    }

    public Algorithm GetAlgorithm()
    {
        return _algorithm is Algorithm result ? result : throw Exceptions.MissingAlgorithm();
    }

    public IAlgorithmEngine GetEngine()
    {
        return _algorithmEngine is IAlgorithmEngine result ? result : throw Exceptions.MissingAlgorithmEngine();
    }

    public AlgoBatch CreateAlgoBatch(long id)
    {
        if (_algorithm == null || _algorithmEngine == null) throw new InvalidOperationException("Must specify algorithm and algo-engine before saving an algo-batch entry.");

        var algoBatch = new AlgoBatch
        {
            Id = id,
            AlgoId = _algorithm.Id,
            AlgoName = _algorithm.GetType().Name,
            AlgoVersionId = _algorithm.VersionId,
            UserId = UserId,
            AccountId = AccountId,
            Environment = Environment,
            AlgorithmParameters = _algorithm.AlgorithmParameters,
            AlgorithmParametersInString = _algorithm.AlgorithmParameters.ToString() + _algorithm.PrintAlgorithmParameters(),
            EngineParameters = _algorithmEngine.EngineParameters,
            EngineParametersInString = _algorithmEngine.EngineParameters.ToString(),
            StartTime = DateTime.UtcNow,
        };
        return algoBatch;
    }

    public bool SetPreferredQuoteCurrencies(List<string>? currencies)
    {
        _preferredQuoteCurrencies.Clear();
        if (currencies.IsNullOrEmpty())
        {
            _log.Warn("No preferred quote currencies are set!");
            return false;
        }
        foreach (var currency in currencies)
        {
            var security = Services.Security.GetSecurity(currency);
            if (security != null)
            {
                _preferredQuoteCurrencies.Add(security);
            }
            else
            {
                _log.Warn($"Invalid preferred quote currency: {currency}; it will be ignored.");
            }
        }
        _log.Info($"Set preferred assets: " + string.Join(", ", _preferredQuoteCurrencies.Select(s => s.Code + "/" + s.Id)));
        return true;
    }

    public bool SetGlobalCurrencyFilter(List<string>? currencies)
    {
        _currencyWhitelist.Clear();
        if (currencies.IsNullOrEmpty())
        {
            _log.Info("No global currency filter is set.");
            return false;
        }
        foreach (var currency in currencies)
        {
            var security = Services.Security.GetSecurity(currency);
            if (security != null)
            {
                _currencyWhitelist.Add(security);
            }
            else
            {
                _log.Warn($"Invalid global currency filter: {currency}; it will be ignored.");
            }
        }
        _log.Info($"Set global currency filter: " + string.Join(", ", _currencyWhitelist.Select(s => s.Code + "/" + s.Id)));
        return true;
    }
}
