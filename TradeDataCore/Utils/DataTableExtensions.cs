using System.Data;

namespace TradeDataCore.Utils
{
    public static class DataTableExtensions
    {
        public static HashSet<T> GetDistinctValues<T>(this DataTable table, string columnName)
        {
            IEnumerable<object> enumerable()
            {
                foreach (DataRow dr in table.Rows)
                {
                    var obj = dr[columnName];
                    yield return obj;
                }
            }

            return enumerable().Distinct().Cast<T>().ToHashSet();
        }
    }
}
