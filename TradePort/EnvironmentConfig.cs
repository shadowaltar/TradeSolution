using Autofac;
using Common;
using log4net;
using TradeCommon.Runtime;

namespace TradePort;

public record EnvironmentConfig
{
    private static readonly ILog _log = Logger.New();

    public string EnvironmentName { get; }
    public EnvironmentType EnvironmentType { get; }

    public string Title { get; }
    public string SubUrl { get; }
    public string AppSettingsFileName { get; }
    public string CustomStylePath { get; }
    public string CookieName { get; }

    public EnvironmentConfig(EnvironmentType environmentType)
    {
        EnvironmentType = environmentType;
        EnvironmentName = environmentType switch
        {
            EnvironmentType.Simulation => "sim",
            EnvironmentType.Test => "test",
            EnvironmentType.Uat => "uat",
            EnvironmentType.Prod => "prod",
            _ => throw new ArgumentException("Invalid environment type", nameof(environmentType))
        };

        SubUrl = "/" + EnvironmentName;
        AppSettingsFileName = $"appsettings.{EnvironmentName}.json";
        Title = $"TradePort ({EnvironmentName.ToUpperInvariant()})";
        CustomStylePath = $"{EnvironmentName}/swagger-custom/{EnvironmentName}-styles.css";
        CookieName = $"TradePort.{EnvironmentName.ToUpperInvariant()}";
    }

    public void RegisterDependencyModule(ContainerBuilder builder)
    {
        switch (EnvironmentType)
        {
            case EnvironmentType.Simulation:
                builder.RegisterModule<TradeConnectivity.CryptoSimulator.Dependencies>();
                _log.Info("Loaded module for: " + nameof(TradeConnectivity.CryptoSimulator));
                break;
            case EnvironmentType.Test:
            case EnvironmentType.Uat:
            case EnvironmentType.Prod:
                builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                _log.Info("Loaded module for: " + nameof(TradeConnectivity.Binance));
                break;
            default:
                throw Exceptions.Invalid<EnvironmentType>("Invalid environment.");
        }
    }
}
