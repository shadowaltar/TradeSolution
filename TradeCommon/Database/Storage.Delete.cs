using Common.Database;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Database;

public partial class Storage
{
    public async Task DeleteOpenOrderId(OpenOrderId openOrderId)
    {
        var tableName = DatabaseNames.OpenOrderIdTable;
        if (!_writers.TryGetValue(DataType.OpenOrderId.ToString(), out var writer))
        {
            writer = new SqlWriter<OpenOrderId>(this, tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.OpenOrderId.ToString()] = writer;
        }
        await writer.DeleteOne(openOrderId);
    }
}
