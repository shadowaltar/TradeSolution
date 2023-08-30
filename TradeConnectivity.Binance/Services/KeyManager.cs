﻿using Common;
using log4net;
using System.Security.Cryptography;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Services;
public class KeyManager
{
    private static readonly ILog _log = Logger.New();

    private const string _secretFolder = @"C:\Temp\Data";

    // key1 is user name, key2 is account name
    private readonly Dictionary<int, Dictionary<int, HMACSHA256>> _testHashers = new();
    private readonly Dictionary<int, Dictionary<int, HMACSHA256>> _uatHashers = new();
    private readonly Dictionary<int, Dictionary<int, HMACSHA256>> _prodHashers = new();

    private readonly Dictionary<int, Dictionary<int, string[]>> _testApiKeys = new();
    private readonly Dictionary<int, Dictionary<int, string[]>> _uatApiKeys = new();
    private readonly Dictionary<int, Dictionary<int, string[]>> _prodApiKeys = new();

    private int _currentUserId;
    private int _currentAccountId;
    private EnvironmentType _currentEnvironment;

    //public string ApiKey { get; private set; } = "gz2ZlwFL6equlgho6rpv05xDE3LvLZ4IAx5iVLSRmcdY1ourA8IJoZBTT5iH47Nx";
    //public string SecretKey { get; private set; } = "oc58o7h4qTNI8BBPHnCUJW1SJ1kiw3qYSFx5j4QGYoufb68lHYIWdqBzFwwpJbrn";
    //public HMACSHA256? Hasher { get; private set; }

    /// <summary>
    /// Sign in current user and account, and use its credentials / keys.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public bool Use(User user, Account account)
    {
        Assertion.ShallNever(Environments.Parse(user.Environment) != account.Environment);
        Assertion.ShallNever(user == null);

        var secretFileName = $"{user!.Environment}_{user.Name}_{account.Name}";
        try
        {
            var path = Path.Combine(_secretFolder, secretFileName);
            var lines = File.ReadAllLines(path);
            if (lines.Length != 2)
            {
                return false;
            }
            if (lines[0].Length != 64 || lines[1].Length != 64)
            {
                return false;
            }
            var apiKey = new[] { lines[0] };
            var secretKey = lines[1];

            Dictionary<int, string[]> apiKeysByAccount;
            Dictionary<int, HMACSHA256> secretHashersByAccount;
            switch (account.Environment)
            {
                case EnvironmentType.Test:
                    apiKeysByAccount = _testApiKeys.GetOrCreate(user.Id);
                    secretHashersByAccount = _testHashers.GetOrCreate(user.Id);
                    break;
                case EnvironmentType.Uat:
                    apiKeysByAccount = _uatApiKeys.GetOrCreate(user.Id);
                    secretHashersByAccount = _uatHashers.GetOrCreate(user.Id);
                    break;
                case EnvironmentType.Prod:
                    apiKeysByAccount = _prodApiKeys.GetOrCreate(user.Id);
                    secretHashersByAccount = _prodHashers.GetOrCreate(user.Id);
                    break;
                default:
                    throw new InvalidOperationException("Invalid environment");
            }
            apiKeysByAccount[account.Id] = apiKey;

            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var hasher = new HMACSHA256(keyBytes);
            secretHashersByAccount[account.Id] = hasher;

            // TODO
            // currently only single user / account is implemented.
            _currentUserId = user.Id;
            _currentAccountId = account.Id;
            _currentEnvironment = account.Environment;

            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to read secret file.", ex);
            return false;
        }
    }

    public byte[] ComputeHash(int userId, int accountId, EnvironmentType environment, string parameterString)
    {
        var secretHashersByAccount = environment switch
        {
            EnvironmentType.Test => _testHashers.GetOrCreate(userId),
            EnvironmentType.Uat => _uatHashers.GetOrCreate(userId),
            EnvironmentType.Prod => _prodHashers.GetOrCreate(userId),
            _ => throw new InvalidOperationException("Invalid environment"),
        };
        var hasher = secretHashersByAccount.GetValueOrDefault(accountId)
            ?? throw new InvalidOperationException("Must load the keys before hash computation.");
        var valueBytes = Encoding.UTF8.GetBytes(parameterString);
        return hasher!.ComputeHash(valueBytes);
    }

    public string[] GetApiKey(int userId, int accountId, EnvironmentType environment)
    {
        var apiKeysByAccount = environment switch
        {
            EnvironmentType.Test => _testApiKeys.GetOrCreate(userId),
            EnvironmentType.Uat => _uatApiKeys.GetOrCreate(userId),
            EnvironmentType.Prod => _prodApiKeys.GetOrCreate(userId),
            _ => throw new InvalidOperationException("Invalid environment"),
        };
        return apiKeysByAccount.TryGetValue(accountId, out var apiKey)
            ? apiKey
            : throw new InvalidOperationException("Must load the keys before hash computation.");
    }

    public byte[] ComputeHash(string parameterString)
    {
        return ComputeHash(_currentUserId, _currentAccountId, _currentEnvironment, parameterString);
    }

    public string[] GetApiKey()
    {
        return GetApiKey(_currentUserId, _currentAccountId, _currentEnvironment);
    }
}

