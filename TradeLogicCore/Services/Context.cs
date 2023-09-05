using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public class Context
{
    private EnvironmentType _environment;
    private ExchangeType _exchange;
    private int _exchangeId;
    private BrokerType _broker;
    private int _brokerId;
    private User? _user;
    private Account? _account;

    /// <summary>
    /// Get current environment.
    /// </summary>
    public EnvironmentType Environment
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _environment;

        private set => _environment = value;
    }

    /// <summary>
    /// Get current exchange for trading.
    /// </summary>
    public ExchangeType Exchange
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _exchange;
        private set => _exchange = value;
    }

    /// <summary>
    /// Get the Id of the <see cref="Exchange"/>.
    /// </summary>
    public int ExchangeId
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _exchangeId;
        private set => _exchangeId = value;
    }

    /// <summary>
    /// Get current broker being connected to.
    /// </summary>
    public BrokerType Broker
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _broker;
        private set => _broker = value;
    }

    /// <summary>
    /// Get the Id of the <see cref="Broker"/>.
    /// </summary>
    public int BrokerId
    {
        get => !IsInitialized ? throw new InvalidOperationException("Must initialize beforehand.") : _brokerId;
        private set => _brokerId = value;
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

    public bool IsInitialized { get; private set; }

    public void Initialize(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        if (environment is EnvironmentType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(environment));
        if (exchange is ExchangeType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(exchange));
        if (broker is BrokerType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(broker));

        IsInitialized = true;

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
