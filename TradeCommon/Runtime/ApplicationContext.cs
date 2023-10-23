using Autofac;
using Common;
using log4net;
using System.Net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Algorithms;

namespace TradeCommon.Runtime;
public class ApplicationContext
{
    protected static readonly ILog _log = Logger.New();
    protected IComponentContext? _container;

    private readonly List<string> _preferredQuoteCurrencies = new();
    private readonly List<string> _globalCurrencyFilter = new();
    private EnvironmentType _environment;
    private ExchangeType _exchange;
    private int _exchangeId;
    private BrokerType _broker;
    private int _brokerId;

    public long AlgoBatchId { get; }

    public bool IsExternalProhibited { get; private set; }

    public bool IsInitialized { get; private set; }

    public IStorage Storage { get; }

    /// <summary>
    /// The preferred quote currencies used by the engine for features like auto-closing.
    /// </summary>
    public IReadOnlyList<string> PreferredQuoteCurrencies => _preferredQuoteCurrencies;

    /// <summary>
    /// If this is set, any assets with currencies not in this filter will be ignored.
    /// </summary>
    public IReadOnlyList<string> GlobalCurrencyFilter => _globalCurrencyFilter;

    public ApplicationContext(IStorage storage)
    {
        Storage = storage;
        AlgoBatchId = IdGenerators.Get<AlgoBatch>().NewTimeBasedId;
    }

    /// <summary>
    /// Get current environment.
    /// </summary>
    public EnvironmentType Environment
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _environment;
        private set => _environment = value;
    }

    /// <summary>
    /// Get current exchange for trading.
    /// </summary>
    public ExchangeType Exchange
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _exchange;
        private set => _exchange = value;
    }

    public int UserId { get; protected set; } = 0;
    public int AccountId { get; protected set; } = 0;

    /// <summary>
    /// Get the Id of the <see cref="Exchange"/>.
    /// </summary>
    public int ExchangeId
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _exchangeId;
        private set => _exchangeId = value;
    }

    /// <summary>
    /// Get current broker being connected to.
    /// </summary>
    public BrokerType Broker
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _broker;
        private set => _broker = value;
    }

    /// <summary>
    /// Get the Id of the <see cref="Broker"/>.
    /// </summary>
    public int BrokerId
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _brokerId;
        private set => _brokerId = value;
    }

    public void Initialize(IComponentContext container, EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        if (environment is EnvironmentType.Unknown) throw Exceptions.EnumUnknown(nameof(environment));
        if (exchange is ExchangeType.Unknown) throw Exceptions.EnumUnknown(nameof(exchange));
        if (broker is BrokerType.Unknown) throw Exceptions.EnumUnknown(nameof(broker));

        _container = container;

        IsInitialized = true;
        IsExternalProhibited = Dns.GetHostName().Contains("PAG");

        Environment = environment;
        Exchange = exchange;
        Broker = broker;
        ExchangeId = ExternalNames.GetExchangeId(exchange);
        BrokerId = ExternalNames.GetBrokerId(broker);

        ExternalQueryStates.Exchange = exchange;
        ExternalQueryStates.Environment = environment;
        ExternalQueryStates.EnvironmentId = ExchangeId;
        ExternalQueryStates.Broker = broker;
        ExternalQueryStates.BrokerId = BrokerId;
        ExternalConnectionStates.Exchange = exchange;
        ExternalConnectionStates.Environment = environment;
        ExternalConnectionStates.EnvironmentId = ExchangeId;
        ExternalConnectionStates.Broker = broker;
        ExternalConnectionStates.BrokerId = BrokerId;
    }

    public bool SetPreferredQuoteCurrencies(List<string>? currencies)
    {
        _preferredQuoteCurrencies.Clear();
        if (currencies.IsNullOrEmpty())
        {
            _log.Warn("No preferred quote currencies are set!");
            return false;
        }
        _log.Info($"Set preferred assets: " + string.Join(", ", currencies));
        _preferredQuoteCurrencies.AddRange(currencies);
        return true;
    }

    public bool SetGlobalCurrencyFilter(List<string>? currencies)
    {
        _globalCurrencyFilter.Clear();
        if (currencies.IsNullOrEmpty())
        {
            _log.Info("No global currency filter is set.");
            return false;
        }
        _log.Info($"Set global currency filter: " + string.Join(", ", currencies));
        _globalCurrencyFilter.AddRange(currencies);
        return true;
    }
}
