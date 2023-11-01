namespace TradeCommon.Utils.Evaluation;
public enum RankingType
{
    None,
    TopN,
    BottomN,
}


public static class RankingTypeConverter
{
    public static RankingType Parse(string? str)
    {
        if (str == null)
            return RankingType.None;

        str = str.Trim().ToUpperInvariant();

        return str switch
        {
            "TOPN" or "TOP-N" => RankingType.TopN,
            "BOTTOMN" or "BOTTOM-N" => RankingType.TopN,
            _ => RankingType.None,
        };
    }

    public static string ToString(RankingType rankingType)
    {
        return rankingType.ToString();
    }
}