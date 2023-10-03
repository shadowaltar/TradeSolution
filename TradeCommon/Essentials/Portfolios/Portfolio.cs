using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including cash-position (uninvested/free cash).
/// </summary>
public record Portfolio
{
    public Dictionary<long, Position> Positions { get; } = new();

    public Dictionary<long, Asset> Assets { get; } = new();

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
            Assets.Add(asset.Id, asset);
        }
        foreach (var position in positions)
        {
            if (position.Security.IsAsset)
                throw Exceptions.InvalidSecurity(position.Security.Code, "Expecting a security for normal position.");
            Positions.Add(position.Id, position);
        }
    }

    public Asset? GetAssetBySecurityId(int securityId)
    {
        return Assets.Values.FirstOrDefault(a => a.SecurityId == securityId);
    }

    public Position? GetPositionBySecurityId(int securityId)
    {
        return Positions.Values.FirstOrDefault(a => a.SecurityId == securityId);
    }

    public virtual bool Equals(Portfolio? portfolio)
    {
        if (portfolio == null) return false;
        if (AccountId != portfolio.AccountId) return false;
        if (Assets.Count != portfolio.Assets.Count) return false;

        foreach (var asset in Assets.Values)
        {
            if (!portfolio.Assets.TryGetValue(asset.Id, out var exists))
                return false;
            if (!exists.Equals(asset))
                return false;
        }
        foreach (var position in Positions.Values)
        {
            if (!portfolio.Assets.TryGetValue(position.Id, out var exists))
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
        return $"Portfolio: acctId {AccountId}, pos {Positions.Count}, asset {Assets.Count}";
    }
}