using Autofac;
using Common;
using log4net;
using System.Net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Runtime;
public class ApplicationContext
{
    protected static readonly ILog _log = Logger.New();

    protected readonly List<Security> _preferredQuoteCurrencies = new();
    protected readonly List<Security> _currencyWhitelist = new();

    protected IComponentContext? _container;

    protected EnvironmentType _environment;
    protected ExchangeType _exchange;
    protected int _exchangeId;
    protected BrokerType _broker;
    protected int _brokerId;

    public bool IsExternalProhibited { get; private set; }

    public bool IsInitialized { get; protected set; }

    public IStorage Storage { get; }

    /// <summary>
    /// The preferred quote currencies used by the engine for features like auto-closing.
    /// </summary>
    public IReadOnlyList<Security> PreferredQuoteCurrencies => _preferredQuoteCurrencies;

    /// <summary>
    /// If this is set, any assets with currencies not in this filter will be ignored.
    /// </summary>
    public IReadOnlyList<Security> CurrencyWhitelist => _currencyWhitelist;

    public bool HasCurrencyWhitelist => _currencyWhitelist.Count != 0;

    public ApplicationContext(IComponentContext container, IStorage storage)
    {
        _container = container;
        Storage = storage;
    }

    /// <summary>
    /// Get current environment.
    /// </summary>
    public EnvironmentType Environment
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _environment;
        protected set => _environment = value;
    }

    /// <summary>
    /// Get current exchange for trading.
    /// </summary>
    public ExchangeType Exchange
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _exchange;
        protected set => _exchange = value;
    }

    public int UserId { get; protected set; } = 0;
    public int AccountId { get; protected set; } = 0;

    /// <summary>
    /// Get the Id of the <see cref="Exchange"/>.
    /// </summary>
    public int ExchangeId
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _exchangeId;
        protected set => _exchangeId = value;
    }

    /// <summary>
    /// Get current broker being connected to.
    /// </summary>
    public BrokerType Broker
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _broker;
        protected set => _broker = value;
    }

    /// <summary>
    /// Get the Id of the <see cref="Broker"/>.
    /// </summary>
    public int BrokerId
    {
        get => !IsInitialized ? throw Exceptions.ContextNotInitialized() : _brokerId;
        protected set => _brokerId = value;
    }

    public void Initialize(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        if (environment is EnvironmentType.Unknown) throw Exceptions.EnumUnknown(nameof(environment));
        if (exchange is ExchangeType.Unknown) throw Exceptions.EnumUnknown(nameof(exchange));
        if (broker is BrokerType.Unknown) throw Exceptions.EnumUnknown(nameof(broker));

        IsInitialized = true;
        IsExternalProhibited = Dns.GetHostName().Contains("PAG");

        Environment = environment;
        Exchange = exchange;
        Broker = broker;
        ExchangeId = ExternalNames.GetExchangeId(exchange);
        BrokerId = ExternalNames.GetBrokerId(broker);

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
}
