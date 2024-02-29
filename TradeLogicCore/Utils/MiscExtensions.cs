namespace TradeLogicCore.Utils;

public static class MiscExtensions
{

    public static List<Asset> Clone<Asset>(this List<Asset> list)
    {
        var result = new List<Asset>(list.Count);
        foreach (Asset item in list)
        {
            result.Add(item with { });
        }
        return result;
    }
}