namespace TradeCommon.Constants;
public static class RestApiConstants
{
    public const string ExecutionRoot = "execution";
    public const string AdminRoot = "admin";

    public const string Login = "login";
    public const string ChangeUserPassword = "change-password";

    public const string SendOrder = "orders/send";
    public const string CancelOrder = "orders/cancel";
    public const string CancelAllOrders = "orders/cancel-all";

    public const string QueryRunningAlgorithms = "algorithms/list";
    public const string QueryAlgoBatches = "algo-batches/list";
    public const string QueryAlgoEntries = "algo-entries/list";
    public const string StartAlgorithmMac = "algorithms/mac/start";
    public const string StopAlgorithm = "algorithms/stop";
    public const string StopAllAlgorithms = "algorithms/stop-all";

    public const string QueryOrders = "orders/list";
    public const string QueryOrderStates = "order-states/list";
    public const string QueryTrades = "trades/list";
    public const string QueryPositions = "positions/list";
    public const string QueryAssets = "assets/list";
    public const string QueryAssetStates = "asset-states/list";
}
