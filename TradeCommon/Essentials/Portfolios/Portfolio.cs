using Common;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including asset-position (uninvested/free cash).
/// Notice that positions here are all open positions; closed ones are removed immediately.
/// </summary>
public record Portfolio
{
    private readonly Dictionary<long, Position> _positions = new();
    private readonly Dictionary<long, Asset> _assets = new();
    private readonly Dictionary<int, Position> _positionsBySecurityId = new();
    private readonly Dictionary<int, Asset> _assetsBySecurityId = new();

    private readonly object _lock = new();

    public int AccountId { get; } = 0;

    public Portfolio(int accountId,
                     List<Position> positions,
                     List<Asset> assets)
    {
        AccountId = accountId;
        foreach (var asset in assets)
        {
            if (!asset.Security.IsAsset)
                throw Exceptions.InvalidSecurity(asset.Security.Code, "Expecting a security for asset position.");
            _assets.Add(asset.Id, asset);
            _assetsBySecurityId.Add(asset.SecurityId, asset);
        }
        foreach (var position in positions)
        {
            if (position.Security.IsAsset)
                throw Exceptions.InvalidSecurity(position.Security.Code, "Expecting a security for normal position.");
            _positions.Add(position.Id, position);
            _positionsBySecurityId.Add(position.SecurityId, position);
        }
    }

    public Position? GetPosition(long id)
    {
        lock (_lock)
            return _positions.GetOrDefault(id);
    }

    public Position? GetPositionBySecurityId(int securityId)
    {
        lock (_lock)
            return _positionsBySecurityId.GetOrDefault(securityId);
    }

    public List<Position> GetPositions()
    {
        lock (_lock)
            return _positions.Values.ToList();
    }

    public List<Asset> GetAssets()
    {
        lock (_lock)
            return _assets.Values.ToList();
    }

    public Asset? GetAsset(long id)
    {
        lock (_lock)
            return _assets.GetOrDefault(id);
    }

    public Asset? GetAssetBySecurityId(int securityId)
    {
        lock (_lock)
            return _assetsBySecurityId.GetOrDefault(securityId);
    }

    public void AddOrUpdate(Position position)
    {
        lock (_lock)
        {
            _positions[position.Id] = position;
            _positionsBySecurityId[position.SecurityId] = position;
        }
    }

    public void Add(Asset asset)
    {
        lock (_lock)
        {
            _assets[asset.Id] = asset;
            _assetsBySecurityId[asset.SecurityId] = asset;
        }
    }

    public bool RemovePosition(long id)
    {
        lock (_lock)
            return _positions.Remove(id);
    }

    public void Clear()
    {
        ClearPositions();
        ClearAssets();
    }

    public void ClearPositions()
    {
        lock (_lock)
        {
            _positions.Clear();
            _positionsBySecurityId.Clear();
        }
    }

    public void ClearAssets()
    {
        lock (_lock)
        {
            _assets.Clear();
            _assetsBySecurityId.Clear();
        }
    }

    public virtual bool Equals(Portfolio? portfolio)
    {
        if (portfolio == null) return false;
        if (AccountId != portfolio.AccountId) return false;
        if (_assets.Count != portfolio._assets.Count) return false;

        foreach (var asset in _assets.Values)
        {
            if (!portfolio._assets.TryGetValue(asset.Id, out var exists))
                return false;
            if (!exists.Equals(asset))
                return false;
        }
        foreach (var position in _positions.Values)
        {
            if (!portfolio._assets.TryGetValue(position.Id, out var exists))
                return false;
            if (!exists.Equals(position))
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
        return $"Portfolio: acctId {AccountId}, pos {_positions.Count}, asset {_assets.Count}";
    }
}