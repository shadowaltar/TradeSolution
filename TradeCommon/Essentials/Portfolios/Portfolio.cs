using TradeCommon.Essentials.Accounts;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including cash-position (uninvested/free cash).
/// </summary>
public record Portfolio
{
    public Dictionary<int, Position> Positions { get; } = new();
    public Dictionary<int, decimal> InitialQuantities { get; } = new();

    public Portfolio(Account account)
    {
        var start = DateTime.UtcNow;
        foreach (var balance in account.Balances)
        {
            var p = new Position
            {
                AccountId = balance.AccountId,
                SecurityId = balance.AssetId,
                Currency = balance.AssetCode,
                Id = -1,
                Price = 0,
                Quantity = balance.FreeAmount,
                LockQuantity = balance.LockedAmount,
                RealizedPnl = 0,
                StartTime = start,
                UpdateTime = start,
            };
            Positions[balance.AssetId] = p;
            InitialQuantities[balance.AssetId] = balance.FreeAmount;
        }
    }

    public decimal GetInitialFreeAmount(int securityId)
    {
        if (InitialQuantities.TryGetValue(securityId, out var q))
        {
            return q;
        }
        return 0;
    }

    public decimal GetNotional(int securityId)
    {
        if (Positions.TryGetValue(securityId, out var p))
        {
            return p.Quantity * p.Price;
        }
        return 0;
    }

    public decimal GetTotalRealizedPnl(int securityId)
    {
        if (Positions.TryGetValue(securityId, out var p))
        {
            return p.RealizedPnl;
        }
        return 0;
    }
}