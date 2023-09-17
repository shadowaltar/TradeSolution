using Microsoft.Identity.Client;
using System;
using TradeCommon.Essentials.Portfolios;

namespace TradeCommon.Runtime;

public static class Exceptions
{
    public static InvalidOperationException InvalidSecurityId(int securityId)
    {
        return new InvalidOperationException($"The security id {securityId} has no security defined. Check your security definition.");
    }

    public static InvalidOperationException InvalidSecurityCode(string code)
    {
        return new InvalidOperationException($"The security code {code} has no security defined. Check your security definition.");
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

    public static Exception InvalidStorageDefinition()
    {
        return new InvalidOperationException("Must specify proper storage info");
    }

    public static Exception InvalidPosition(long positionId, string message)
    {
        return new InvalidOperationException($"The position (id: {positionId}) is invalid: {message}.");
    }

    public static Exception MissingSecurity()
    {
        return new InvalidOperationException($"The security is missing.");
    }
}
