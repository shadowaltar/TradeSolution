namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including cash-position (uninvested/free cash).
/// </summary>
public record Portfolio
{
    public Dictionary<long, Position> Positions { get; } = new();

    public Dictionary<long, Asset> Assets { get; } = new();

    public int AccountId { get; } = 0;

    public Portfolio(int accountId, List<Position> positions)
    {
        var start = DateTime.UtcNow;
        foreach (var position in positions)
        {
            if (position.Security.IsAsset)
            {
                Assets.Add(position.Id, position);
            }
            else
            {
                Positions.Add(position.Id, position);
            }

            // position = new Position
            //{
            //    Id = 0,
            //    AccountId = position.AccountId,
            //    Security = position.Security,
            //    SecurityId = position.SecurityId,
            //    SecurityCode = position.SecurityCode,
            //    Price = 0,
            //    Quantity = position.Quantity,
            //    LockedQuantity = position.LockedQuantity,
            //    StrategyLockedQuantity = position.StrategyLockedQuantity,
            //    CreateTime = start,
            //    UpdateTime = start,
            //    CloseTime = DateTime.MaxValue,
            //    StartNotional = 0, // price unknown
            //    Notional = 0,
            //    StartOrderId = 0,
            //    EndOrderId = 0,
            //    StartTradeId = 0,
            //    EndTradeId = 0,
            //    RealizedPnl = 0,
            //    AccumulatedFee = 0,
            //};
            //Assets[position.SecurityId] = position;
        }
        AccountId = accountId;
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
}