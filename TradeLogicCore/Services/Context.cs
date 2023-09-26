using Autofac;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;
public class Context : ApplicationContext
{
    private User? _user;
    private Account? _account;
    private IServices? _services;
    private IAlgorithm? _algorithm;
    private IAlgorithmEngine? _algorithmEngine;

    public Context(IStorage storage) : base(storage)
    {
    }

    public bool IsBackTesting
    {
        get
        {
            if (_algorithmEngine?.Parameters == null) throw Exceptions.InvalidAlgorithmEngineState();
            return _algorithmEngine.Parameters.IsBackTesting;
        }
    }

    public IServices Services
    {
        get
        {
            _services ??= _container?.Resolve<IServices>() ?? throw Exceptions.ContextNotInitialized();
            return _services;
        }
    }

    /// <summary>
    /// Get the current user (assigned after login).
    /// </summary>
    public User? User
    {
        get => !IsInitialized ? throw Exceptions.MustLogin() : _user;
        internal set => _user = value;
    }

    /// <summary>
    /// Get the current account (assigned after login).
    /// </summary>
    public Account? Account
    {
        get => !IsInitialized ? throw Exceptions.MustLogin() : _account;
        internal set
        {
            if (value == null)
                throw Exceptions.InvalidAccount();
            _account = value;
            AccountId = value.Id;
        }
    }

    public void InitializeAlgorithmContext<T>(IAlgorithmEngine algorithmEngine, IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        _algorithmEngine = algorithmEngine;
        _algorithm = algorithm;
    }

    public IAlgorithm GetAlgorithm()
    {
        return _algorithm is IAlgorithm result ? result : throw Exceptions.MissingAlgorithm();
    }

    public IAlgorithmEngine GetEngine()
    {
        return _algorithmEngine is IAlgorithmEngine result ? result : throw Exceptions.MissingAlgorithmEngine();
    }
}
