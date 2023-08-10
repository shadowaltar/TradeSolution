using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeDataCore.StaticData;

namespace TradeLogicCore.Services;
public class AdminService : IAdminService
{
    public async Task<int> CreateUser(string userName, string userPassword, string email)
    {
        if (userName.IsBlank() || userPassword.IsBlank() || email.IsBlank())
        {
            return -1;
        }
        userName = userName.Trim().ToLowerInvariant();
        userPassword = userPassword.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();

        var user = new User
        {
            Name = userName,
            Email = email,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };
        Credential.EncryptUserPassword(user, ref userPassword);

        return await Storage.InsertUser(user);
    }

    public async Task<User?> ReadUser(string userName)
    {
        return await Storage.ReadUser(userName);
    }

    public async Task<User?> ReadUserByEmail(string email)
    {
        return await Storage.ReadUser("", email);
    }
}
