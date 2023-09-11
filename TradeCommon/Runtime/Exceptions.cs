namespace TradeCommon.Runtime;

public static class Exceptions
{
    public static InvalidOperationException ThrowMissingQuoteAsset(string securityCode)
    {
        return new InvalidOperationException($"The security {securityCode} must have an associated currency/quote asset. Check your security definition.");
    }

    public static InvalidOperationException ThrowMissingAssetPosition(string assetCode)
    {
        return new InvalidOperationException($"The portfolio must hold an asset with code = {assetCode}.");
    }
}
