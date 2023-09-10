using TradeCommon.Essentials.Accounts;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including cash-position (uninvested/free cash).
/// </summary>
public record Portfolio
{
    public Dictionary<int, Position> Positions { get; } = new();

    public Dictionary<int, Position> Assets { get; } = new();

    public Portfolio(Account account)
    {
        var start = DateTime.UtcNow;
        foreach (var balance in account.Balances)
        {
            var position = new Position
            {
                Id = -1,
                AccountId = balance.AccountId,
                SecurityId = balance.AssetId,
                SecurityCode = balance.AssetCode,
                BaseAssetId = balance.AssetId,
                QuoteAssetId = balance.AssetId,
                Price = 0,
                Quantity = balance.FreeAmount,
                LockQuantity = balance.LockedAmount,
                RealizedPnl = 0,
                StartTime = start,
                UpdateTime = start,
            };
            Assets[position.SecurityId] = position;
        }
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