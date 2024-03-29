﻿namespace TradeCommon.Constants;
public static class RestApiConstants
{
    public const string ExecutionRoot = "execution";
    public const string AdminRoot = "admin";
    public const string QuotationRoot = "quotation";
    public const string Static = "static";

    public const string Login = "login";
    public const string Logout = "logout";
    public const string ChangeUserPassword = "change-password";
    public const string Reconcile = "reconcile";
    public const string Securities = "securities";

    public const string SendOrder = "orders/send";
    public const string CancelOrder = "orders/cancel";
    public const string CancelAllOrders = "orders/cancel-all";

    public const string QueryRunningAlgoSessions = "algorithms/list";
    public const string QueryAlgoSessions = "algo-sessions/list";
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
