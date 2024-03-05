using Common;
using log4net;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including asset-position (uninvested/free cash).
/// Notice that positions here are all open positions; closed ones are removed immediately.
/// </summary>
public record Portfolio(int AccountId)
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<int, Asset> _cashPositionsBySecurityId = [];
    private readonly Dictionary<int, Asset> _assetsBySecurityId = [];

    private readonly object _lock = new();

    /// <summary>
    /// Gets all asset positions, including those quantity == 0.
    /// </summary>
    public bool HasAssetPosition => _assetsBySecurityId.Count > 0;

    public Portfolio(int accountId, List<Asset> assets) : this(accountId)
    {
        foreach (var asset in assets)
        {
            if (asset.Quantity > 0)
            {
                if (asset.Security.IsCash)
                    _cashPositionsBySecurityId[asset.SecurityId] = asset;
                else
                    _assetsBySecurityId[asset.SecurityId] = asset;
            }
        }
    }

    public List<Asset> GetAll()
    {
        lock (_lock)
            return _assetsBySecurityId.Values.Union(_cashPositionsBySecurityId.Values).ToList();
    }

    public List<Asset> GetAssetPositions()
    {
        return _assetsBySecurityId.ThreadSafeValues(_lock);
    }

    public List<Asset> GetCashes()
    {
        return _cashPositionsBySecurityId.ThreadSafeValues(_lock);
    }

    public Asset? GetAssetPositionBySecurityId(int securityId)
    {
        return _assetsBySecurityId.ThreadSafeGet(securityId, _lock);
    }

    public Asset? GetCashAssetBySecurityId(int securityId)
    {
        return _cashPositionsBySecurityId.ThreadSafeGet(securityId, _lock);
    }


    public void AddOrUpdate(Asset asset)
    {
        lock (_lock)
        {
            if (asset.Security == null)
            {
                _log.Error("Invalid asset with no security.");
                return;
            }
            if (asset.Security.IsCash)
                _cashPositionsBySecurityId[asset.SecurityId] = asset;
            else
                _assetsBySecurityId[asset.SecurityId] = asset;
        }
    }

    public void Close(int securityId)
    {
        _assetsBySecurityId.ThreadSafeRemove(securityId, _lock);
    }

    public void Clear()
    {
        ClearPositions();
        ClearCashes();
    }

    public void ClearPositions()
    {
        _assetsBySecurityId.ThreadSafeClear(_lock);
    }

    public void ClearCashes()
    {
        _cashPositionsBySecurityId.ThreadSafeClear(_lock);
    }

    public virtual bool Equals(Portfolio? portfolio)
    {
        if (portfolio == null) return false;
        if (AccountId != portfolio.AccountId) return false;
        if (_assetsBySecurityId.Count != portfolio._assetsBySecurityId.Count) return false;
        if (_cashPositionsBySecurityId.Count != portfolio._cashPositionsBySecurityId.Count) return false;

        foreach (var asset in _assetsBySecurityId.Values)
        {
            if (!portfolio._assetsBySecurityId.TryGetValue(asset.SecurityId, out var exists))
                return false;
            if (!exists.Equals(asset))
                return false;
        }
        foreach (var cash in _cashPositionsBySecurityId.Values)
        {
            if (!portfolio._cashPositionsBySecurityId.TryGetValue(cash.SecurityId, out var exists))
                return false;
            if (!exists.Equals(cash))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        return AccountId.GetHashCode();
    }

    public override string ToString()
    {
        return $"Portfolio: acctId {AccountId}, posi {_assetsBySecurityId.Count}, cash {_cashPositionsBySecurityId.Count}";
    }
}