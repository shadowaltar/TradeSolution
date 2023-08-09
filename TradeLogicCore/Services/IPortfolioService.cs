﻿using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;

namespace TradeLogicCore.Services;
public interface IPortfolioService
{
    event Action<Position>? PositionCreated;
    event Action<Position>? PositionUpdated;
    event Action<Position>? PositionClosed;

    IExternalAccountManagement ExternalAccountManagement { get; }
    IExternalExecutionManagement ExternalExecution { get; }

    /// <summary>
    /// Gets the remaining balance which is free to be traded.
    /// </summary>
    decimal RemainingBalance { get; }

    Task Initialize();

    Task<Account> GetAccountByName(string accountName);

    List<Position> GetOpenPositions();

    List<Position> GetPositions(string externalName, SecurityType securityType);

    /// <summary>
    /// TODO revise
    /// </summary>
    /// <param name="externalName"></param>
    /// <returns></returns>
    List<Balance> GetExternalBalances(string externalName);

    /// <summary>
    /// TODO revise
    /// </summary>
    /// <returns></returns>
    List<Balance> GetCurrentBalances();

    List<ProfitLoss> GetRealizedPnl(Security security, DateTime rangeStart, DateTime rangeEnd);

    ProfitLoss GetUnrealizedPnl(Security security);

    bool Validate(Order order);

    /// <summary>
    /// Select a user in order to use its account / balance / credentials.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    bool SelectUser(User user);
}
