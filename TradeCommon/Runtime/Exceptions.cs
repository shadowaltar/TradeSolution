using System.Runtime.CompilerServices;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Runtime;

public static class Exceptions
{
    public static InvalidOperationException InvalidSecurityId(int securityId)
    {
        return new InvalidOperationException($"The security id {securityId} has no security defined. Check your security definition.");
    }

    public static InvalidOperationException InvalidSecurityCode(string? code)
    {
        return new InvalidOperationException($"The security code {code ?? "n/a"} has no security defined. Check your security definition.");
    }

    public static InvalidOperationException InvalidSecurity(string? code, string message)
    {
        return new InvalidOperationException($"The security with code {code ?? "n/a"} is invalid: " + message);
    }

    public static InvalidOperationException MissingQuoteAsset(int securityId)
    {
        return new InvalidOperationException($"The security with id {securityId} must have an associated currency/quote asset. Check your security definition.");
    }

    public static InvalidOperationException MissingQuoteAsset(string securityCode)
    {
        return new InvalidOperationException($"The security {securityCode} must have an associated currency/quote asset. Check your security definition.");
    }

    public static InvalidOperationException MissingAssetPosition(string assetCode)
    {
        return new InvalidOperationException($"The portfolio must hold an asset with code = {assetCode}.");
    }

    public static InvalidOperationException MissingAssetPosition(Security relatedSecurity)
    {
        return new InvalidOperationException($"The portfolio must hold an asset which is used for security = {relatedSecurity.Code}/{relatedSecurity.Id}.");
    }

    public static InvalidOperationException MissingAsset(int assetId)
    {
        return new InvalidOperationException($"The portfolio must hold an asset with id = {assetId}.");
    }

    public static Exception InvalidSide()
    {
        return new InvalidOperationException($"Must specify buy(long) or sell(short) here.");
    }

    public static Exception InvalidBackTestMode(bool mustBeInBackTestMode)
    {
        return mustBeInBackTestMode
            ? new InvalidOperationException($"Must be in back-test mode here.")
            : (Exception)new InvalidOperationException($"Must not be in back-test mode here.");
    }

    public static Exception MissingBalance(int accountId, int assetId)
    {
        return new InvalidOperationException($"Asset entry should exists: Account {accountId}; Asset {assetId}.");
    }

    public static Exception ContextNotInitialized()
    {
        return new InvalidOperationException($"Context is not initialized.");
    }

    public static Exception MissingAlgorithm()
    {
        return new InvalidOperationException($"Algorithm is not set properly.");
    }

    public static Exception MissingAlgorithmEngine()
    {
        return new InvalidOperationException($"Algorithm engine is not set properly.");
    }

    public static Exception InvalidTimeRange(DateTime? start, DateTime? end)
    {
        return new InvalidOperationException($"Time range is invalid; start {start}, end {end}");
    }

    public static Exception InvalidAlgorithmEngineState()
    {
        return new InvalidOperationException("Algorithm engine is not initialized.");
    }

    public static Exception EnumUnknown(string variableName)
    {
        return new ArgumentException("Must not be unknown.", variableName);
    }

    public static Exception MustLogin()
    {
        return new InvalidOperationException("Must login first.");
    }

    public static Exception InvalidAccount()
    {
        return new InvalidOperationException("Account missing or invalid.");
    }

    public static Exception InvalidStorageDefinition()
    {
        return new InvalidOperationException("Must specify proper storage info");
    }

    public static Exception InvalidPosition(long? positionId, string message)
    {
        return new InvalidOperationException($"The position (id: {positionId?.ToString() ?? "missing"}) is invalid: {message}.");
    }

    public static Exception MissingSecurity()
    {
        return new InvalidOperationException($"The security is missing.");
    }

    public static Exception InvalidTradeServiceState(string message)
    {
        return new InvalidOperationException("Invalid TradeService State: " + message);
    }

    public static Exception InvalidOrderServiceState(string message)
    {
        return new InvalidOperationException("Invalid OrderService State: " + message);
    }

    public static Exception InvalidTradePositionCombination(string message)
    {
        return new InvalidOperationException(message);
    }

    public static Exception Invalid<T>(object? message, [CallerArgumentExpression(nameof(message))] string? argName = null)
    {
        return new InvalidOperationException($"Invalid {typeof(T).Name} from parameter {argName}: {message}");
    }

    public static Exception Invalid(object? message, [CallerArgumentExpression(nameof(message))] string? argName = null)
    {
        return new InvalidOperationException($"Invalid case from parameter {argName}: {message}");
    }

    public static Exception InvalidOrder(object? message)
    {
        return new InvalidOperationException($"Invalid order parameters: {message}");
    }

    public static Exception Impossible(object? message = null)
    {
        return message == null
            ? new InvalidOperationException("Impossible case is hit.")
            : (Exception)new InvalidOperationException("Impossible case is hit: " + message);
    }

    public static Exception InvalidSecurityInTrades()
    {
        return new InvalidOperationException("Trades must with the same security.");
    }

    public static Exception InvalidSecurityInPosition(long id)
    {
        return new InvalidOperationException($"Security in position (with id {id})is invalid.");
    }

    public static Exception NotImplemented(string message = "")
    {
        return new NotImplementedException(message);
    }

    public static Exception Unreachable(BrokerType broker)
    {
        return new InvalidOperationException($"Broker {broker} is unreachable.");
    }
}
