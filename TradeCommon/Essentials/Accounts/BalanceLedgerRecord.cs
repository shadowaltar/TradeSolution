namespace TradeCommon.Essentials.Accounts;

/// <summary>
/// Class to record any actions done onto the asset entries of an account.
/// </summary>
/// <param name="UserId">Who performs this action.</param>
/// <param name="AccountId">To which account this action performs on.</param>
/// <param name="Action">What action which has been done.</param>
/// <param name="AssetId">What asset has been involved.</param>
/// <param name="Value">How much asset has been involved. Must be zero or positive.</param>
public record BalanceLedgerRecord(int UserId, BalanceActionType Action, int AccountId, int AssetId, decimal Value);
