using Autofac;
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

    public bool IsBackTesting => _algorithmEngine?.Parameters.IsBackTesting ?? true;

    public IServices Services
    {
        get
        {
            _services ??= _container.Resolve<IServices>();
            return _services;
        }
    }

    /// <summary>
    /// Get the current user (assigned after login).
    /// </summary>
    public User? User
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _user;
        internal set => _user = value;
    }

    /// <summary>
    /// Get the current account (assigned after login).
    /// </summary>
    public Account? Account
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _account;
        internal set => _account = value;
    }

    public void InitializeAlgorithmContext<T>(IAlgorithmEngine<T> algorithmEngine, IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        _algorithmEngine = algorithmEngine;
        _algorithm = algorithm;
    }

    public IAlgorithm<T> GetAlgorithm<T>() where T : IAlgorithmVariables
    {
        return _algorithm is IAlgorithm<T> result ? result : throw Exceptions.MissingAlgorithm();
    }

    public IAlgorithmEngine<T> GetEngine<T>() where T : IAlgorithmVariables
    {
        return _algorithmEngine is IAlgorithmEngine<T> result ? result : throw Exceptions.MissingAlgorithmEngine();
    }
}
