using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Database;

public partial class Storage
{

    public static async Task DeleteOpenOrderId(OpenOrderId openOrderId)
    {
        var tableName = DatabaseNames.OpenOrderIdTable;
        if (!_writers.TryGetValue(DataType.OpenOrderId, out var writer))
        {
            writer = new SqlWriter<OpenOrderId>(tableName, DatabaseFolder, DatabaseNames.ExecutionData);
            _writers[DataType.OpenOrderId] = writer;
        }
        await writer.DeleteOne(openOrderId);
    }
}
