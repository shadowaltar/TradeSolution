using Autofac;
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
    private IServices? _services;
    private Algorithm? _algorithm;
    private IAlgorithmEngine? _algorithmEngine;

    public Context(IComponentContext container, IStorage storage) : base(container, storage)
    {
    }

    public bool IsBackTesting => _algorithmEngine?.AlgoParameters == null
                ? throw Exceptions.InvalidAlgorithmEngineState()
                : _algorithmEngine.AlgoParameters.IsBackTesting;

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
        internal set
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
        internal set
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

    public Algorithm GetAlgorithm()
    {
        return _algorithm is Algorithm result ? result : throw Exceptions.MissingAlgorithm();
    }

    public IAlgorithmEngine GetEngine()
    {
        return _algorithmEngine is IAlgorithmEngine result ? result : throw Exceptions.MissingAlgorithmEngine();
    }

    public AlgoBatch SaveAlgoBatch()
    {
        if (_algorithm == null || _algorithmEngine == null) throw new InvalidOperationException("Must specify algorithm and algo-engine before saving an algo-batch entry.");

        var algoBatch = new AlgoBatch
        {
            Id = AlgoBatchId,
            AlgoId = _algorithm.Id,
            AlgoName = _algorithm.GetType().Name,
            AlgoVersionId = _algorithm.VersionId,
            UserId = UserId,
            AccountId = AccountId,
            Environment = Environment,
            AlgorithmParameters = _algorithm.AlgorithmParameters,
            EngineParameters = _algorithmEngine.EngineParameters,
            StartTime = DateTime.UtcNow,
        };
        Services.Persistence.Insert(algoBatch, isSynchronous: true);
        return algoBatch;
    }
}
