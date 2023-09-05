using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public class Context
{
    /// <summary>
    /// Get current environment.
    /// </summary>
    public EnvironmentType Environment { get; private set; }

    /// <summary>
    /// Get current exchange for trading.
    /// </summary>
    public ExchangeType Exchange { get; private set; }
    /// <summary>
    /// Get the Id of the <see cref="Exchange"/>.
    /// </summary>
    public int ExchangeId { get; private set; }

    /// <summary>
    /// Get current broker being connected to.
    /// </summary>
    public BrokerType Broker { get; private set; }
    /// <summary>
    /// Get the Id of the <see cref="Broker"/>.
    /// </summary>
    public int BrokerId { get; private set; }

    /// <summary>
    /// Get the current user (assigned after login).
    /// </summary>
    public User? User { get; internal set; }

    /// <summary>
    /// Get the current account (assigned after login).
    /// </summary>
    public Account? Account { get; internal set; }

    public void Setup(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
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
