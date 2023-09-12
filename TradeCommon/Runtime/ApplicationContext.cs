using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;

namespace TradeCommon.Runtime;
public class ApplicationContext
{
    private EnvironmentType _environment;
    private ExchangeType _exchange;
    private int _exchangeId;
    private BrokerType _broker;
    private int _brokerId;

    public ApplicationContext() { }
    public bool IsExternalProhibited { get; private set; }
    public bool IsInitialized { get; private set; }

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

    public void Initialize(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        if (environment is EnvironmentType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(environment));
        if (exchange is ExchangeType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(exchange));
        if (broker is BrokerType.Unknown) throw new ArgumentException("Must not be unknown.", nameof(broker));

        IsInitialized = true;
        IsExternalProhibited = System.Net.Dns.GetHostName().Contains("PAG");
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
