using Autofac;
using Common;
using log4net;
using System.Net;
using TradeCommon.Constants;
using TradeCommon.Database;

namespace TradeCommon.Runtime;
public class ApplicationContext
{
    protected static readonly ILog _log = Logger.New();
    protected IComponentContext? _container;

    private readonly List<string> _preferredAssetCodes = new();
    private EnvironmentType _environment;
    private ExchangeType _exchange;
    private int _exchangeId;
    private BrokerType _broker;
    private int _brokerId;

    public string AlgoSessionId { get; } = "";

    public bool IsExternalProhibited { get; private set; }

    public bool IsInitialized { get; private set; }

    public IStorage Storage { get; }

    public IReadOnlyList<string> PreferredAssetCodes => _preferredAssetCodes;

    public ApplicationContext(IStorage storage)
    {
        Storage = storage;
        AlgoSessionId = Guid.NewGuid().ToString();
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

    public bool SetPreferredAssets(params string[] assetCodes)
    {
        _log.Info($"Set preferred assets: " + string.Join(", ", assetCodes));
        _preferredAssetCodes.Clear();
        _preferredAssetCodes.AddRange(assetCodes);
        if (_preferredAssetCodes.IsNullOrEmpty())
        {
            _preferredAssetCodes.Add("TUSD");
            _preferredAssetCodes.Add("USDT");
            _preferredAssetCodes.Add("BUSD");
            _preferredAssetCodes.Add("USD");
        }
        return true;
    }
}
