﻿using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;
public interface IPortfolioService
{
    event Action<Asset, Trade>? AssetProcessed;
    //event Action<Asset>? AssetPositionCreated;
    //event Action<Asset>? AssetPositionUpdated;
    //event Action<List<Asset>>? AssetPositionsUpdated;
    //event Action<Asset>? AssetClosed;

    Portfolio InitialPortfolio { get; }

    Portfolio Portfolio { get; }

    bool HasAssetPosition { get; }

    /// <summary>
    /// Initialize portfolio service.
    /// Expected to be executed after account and user are specified
    /// (usually via <see cref="IAdminService.Login(string, string?, string?, TradeCommon.Runtime.EnvironmentType)"/>).
    /// </summary>
    /// <returns></returns>
    Task<bool> Initialize();

    Task Reset();

    /// <summary>
    /// Gets all open positions (a shallow copy of list of positions).
    /// </summary>
    /// <returns></returns>
    List<Asset> GetAssets();

    /// <summary>
    /// Gets all cash positions.
    /// </summary>
    /// <returns></returns>
    List<Asset> GetCashes();

    /// <summary>
    /// Gets all cash + non-cash positions.
    /// </summary>
    /// <returns></returns>
    List<Asset> GetAllAssets();

    /// <summary>
    /// Get asset position given its security id.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="isInit"></param>
    /// <returns></returns>
    Asset? GetAssetBySecurityId(long securityId, bool isInit = false);

    /// <summary>
    /// Get asset position given its security Id.
    /// </summary>
    /// <param name="securityId"></param>
    /// <param name="isInit"></param>
    /// <returns></returns>
    Asset? GetCashAssetBySecurityId(long securityId, bool isInit = false);

    /// <summary>
    /// Get the related cash position from a security's <see cref="Security.QuoteSecurity"/>.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Asset? GetRelatedCashPosition(Security security);

    Side GetOpenPositionSide(long securityId);

    bool Validate(Order order);

    ///// <summary>
    ///// Deposit assets to current account's specific asset indicated by <paramref name="assetId"/>.
    ///// If asset is not found, throws exception.
    ///// </summary>
    ///// <param name="assetId"></param>
    ///// <param name="quantity"></param>
    ///// <returns></returns>
    //Task<Asset?> Deposit(int assetId, decimal quantity);

    ///// <summary>
    ///// Deposit assets to specific account's specific asset indicated by <paramref name="accountId"/> and <paramref name="assetId"/>.
    ///// If targeting current account and asset is not found, throws exception; otherwise returns null.
    ///// </summary>
    ///// <param name="accountId"></param>
    ///// <param name="assetId"></param>
    ///// <param name="quantity"></param>
    ///// <returns></returns>
    //Task<Asset?> Deposit(int accountId, int assetId, decimal quantity);

    //Task<Asset?> Withdraw(int assetId, decimal quantity);

    /// <summary>
    /// Traverse through active positions / non-cash assets,
    /// create corresponding opposite side orders and send.
    /// </summary>
    Task<bool> CloseAllPositions(OrderActionType action, string orderComment);

    //Task<bool> CleanUpNonCashAssets(string orderComment);

    Task<List<Asset>> GetExternalAssets();

    Task<List<Asset>> GetStorageAssets();

    Task<List<AssetState>> GetAssetStates(Security security, DateTime start);

    void Update(List<Asset> assets, bool isInitializing = false);

    /// <summary>
    /// Reload service cache.
    /// Both asset position and cash position will be cached.
    /// Can either clear the cache only, or clear + reload from storage.
    /// Can also affect initial portfolio object.
    /// </summary>
    /// <param name="clearOnly"></param>
    /// <param name="affectInitialPortfolio"></param>
    Task Reload(bool clearOnly, bool affectInitialPortfolio);

    decimal GetAssetPositionResidual(long assetSecurityId);
}
