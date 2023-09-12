using Microsoft.Identity.Client;

namespace TradeCommon.Runtime;

public static class Exceptions
{
    public static InvalidOperationException InvalidSecurityId(int securityId)
    {
        return new InvalidOperationException($"The security id {securityId} has no related security defined. Check your security definition.");
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

    public static InvalidOperationException MissingAssetPosition(int assetId)
    {
        return new InvalidOperationException($"The portfolio must hold an asset with id = {assetId}.");
    }

    public static Exception InvalidSide()
    {
        return new InvalidOperationException($"Must specify buy(long) or sell(short) here.");
    }

    public static Exception InvalidBackTestMode(bool mustBeInBackTestMode)
    {
        if (mustBeInBackTestMode)
            return new InvalidOperationException($"Must be in back-test mode here.");
        return new InvalidOperationException($"Must not be in back-test mode here.");
    }

    public static Exception MissingBalance(int accountId, int assetId)
    {
        return new InvalidOperationException($"Balance entry should exists: Account {accountId}; Asset {assetId}.");
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
}
