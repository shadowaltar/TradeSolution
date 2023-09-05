using TradeCommon.Runtime;
using Common.Attributes;

namespace TradeCommon.Essentials.Portfolios;
public class TransferAction
{
    public ActionType Action { get; set; }
    public int AssetId { get; set; }
    
    [UpsertIgnore, SelectIgnore]
    public string? AssetCode { get; set; }
    public decimal Quantity { get; set; }
    public DateTime RequestTime { get; set; }
    public DateTime EffectiveTime { get; set; }
}
