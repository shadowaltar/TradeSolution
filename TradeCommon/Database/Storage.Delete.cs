using Common;
using Common.Database;
using TradeCommon.Constants;
using TradeCommon.Essentials.Trading;

namespace TradeCommon.Database;

public partial class Storage
{
    public async Task DeleteOpenOrderId(OpenOrderId openOrderId)
    {
        var writer = _writers.GetOrCreate(DataType.OpenOrderId.ToString(), () => new SqlWriter<OpenOrderId>(this), (k, w) => Register(w));
        await writer.DeleteOne(openOrderId);
    }
}
