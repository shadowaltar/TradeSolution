using Autofac;
using System.Net;
using TradeCommon.Constants;

namespace TradeCommon.Runtime;
public class ApplicationContext
{
    protected IComponentContext? _container;

    private EnvironmentType _environment;
    private ExchangeType _exchange;
    private int _exchangeId;
    private BrokerType _broker;
    private int _brokerId;

    public bool IsExternalProhibited { get; private set; }

    public bool IsInitialized { get; private set; }

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
}
