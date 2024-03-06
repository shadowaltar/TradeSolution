using Autofac;
using Common;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;
public class Context(IComponentContext container, IStorage storage) : ApplicationContext(container, storage)
{
    private User? _user;
    private Account? _account;
    private Core? _core;
    private IServices? _services;
    private Algorithm? _algorithm;
    private IAlgorithmEngine? _algorithmEngine;

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

    public AlgoSession? AlgoSession { get; internal set; }

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
        // do not reset environment, exchange, broker
        // as they are initialized during app startup

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
    }

    public Algorithm GetAlgorithm()
    {
        return _algorithm is Algorithm result ? result : throw Exceptions.MissingAlgorithm();
    }

    //public IAlgorithmEngine GetEngine()
    //{
    //    return _algorithmEngine is IAlgorithmEngine result ? result : throw Exceptions.MissingAlgorithmEngine();
    //}


    /// <summary>
    /// Set the global security code whitelist.
    /// </summary>
    /// <param name="codes"></param>
    /// <returns></returns>
    public bool SetSecurityCodeWhiteList(List<string>? codes)
    {
        return SetSecurityCodeList(ref _currencyWhitelist, codes, "global code filter");
    }

    /// <summary>
    /// Set the list of asset codes which are cash;
    /// holding them will not be consider holding an active position.
    /// </summary>
    /// <param name="codes"></param>
    /// <returns></returns>
    public bool SetCashCurrencies(List<string> codes)
    {
        if (_services == null)
            throw Exceptions.ContextNotInitialized();
        if (SetSecurityCodeList(ref _cashCurrencies, codes, "cash code"))
        {
            foreach (var code in codes)
            {
                var security = _services.Security.GetSecurity(code);
                if (security == null)
                {
                    _log.Error($"Security code {code} is not found! Cannot set it as cash currency. This code will be ignored.");
                }
                else
                {
                    security.IsCash = true;
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Set the preferred quote currency security code, in case when
    /// trading a currency pair only the base currency code is specified.
    /// </summary>
    /// <param name="codes"></param>
    /// <returns></returns>
    public bool SetPreferredQuoteCurrencies(List<string>? codes)
    {
        return SetSecurityCodeList(ref _preferredQuoteCurrencies, codes, "preferred quote currencies");
    }

    private bool SetSecurityCodeList(ref List<Security> collection, List<string>? securityCodes, string description)
    {
        collection.Clear();

        if (securityCodes.IsNullOrEmpty())
        {
            _log.Warn($"No {description} are set!");
            return false;
        }
        foreach (var code in securityCodes)
        {
            var security = Services.Security.GetSecurity(code);
            if (security != null)
            {
                collection.Add(security);
            }
            else
            {
                _log.Warn($"Invalid {description}: {code}; it will be ignored.");
            }
        }
        _log.Info($"Set {description}: " + string.Join(", ", collection.Select(s => s.Code + "/" + s.Id)));
        return true;
    }
}
