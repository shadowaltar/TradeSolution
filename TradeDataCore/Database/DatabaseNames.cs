using TradeDataCore.Essentials;

namespace TradeDataCore.Database
{
    public static class DatabaseNames
    {
        public const string StaticData = "static_data";
        public const string MarketData = "market_data";

        public const string StockDefinitionTable = "stock_definitions";
        public const string FxDefinitionTable = "fx_definitions";

        public const string StockPrice1mTable = "stock_prices_1m";
        public const string StockPrice1hTable = "stock_prices_1h";
        public const string StockPrice1dTable = "stock_prices_1d";
        public const string FxPrice1mTable = "fx_prices_1m";
        public const string FxPrice1hTable = "fx_prices_1h";
        public const string FxPrice1dTable = "fx_prices_1d";

        public static string GetDefinitionTableName(SecurityType type)
        {
            if (type == SecurityType.Equity)
            {
                return StockDefinitionTable;
            }
            else if (type == SecurityType.Fx)
            {
                return FxDefinitionTable;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static string GetPriceTableName(IntervalType intervalType, SecurityType securityType)
        {
            string tableName;
            switch (intervalType)
            {
                case IntervalType.OneDay:
                    if (securityType == SecurityType.Equity)
                        tableName = StockPrice1dTable;
                    else if (securityType == SecurityType.Fx)
                        tableName = FxPrice1dTable;
                    else
                        throw new NotImplementedException();
                    break;
                case IntervalType.OneHour:
                    if (securityType == SecurityType.Equity)
                        tableName = StockPrice1hTable;
                    else if (securityType == SecurityType.Fx)
                        tableName = FxPrice1hTable;
                    else
                        throw new NotImplementedException();
                    break;
                case IntervalType.OneMinute:
                    if (securityType == SecurityType.Equity)
                        tableName = StockPrice1mTable;
                    else if (securityType == SecurityType.Fx)
                        tableName = FxPrice1mTable;
                    else
                        throw new NotImplementedException();
                    break;
                default: throw new NotImplementedException();
            }
            return tableName;
        }

        public const string FinancialStatsTable = "financial_stats";
    }
}
