using Common;
using log4net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;
using TradeLogicCore.Services;

namespace TradePort.Utils;

public class Authentication
{
    private static readonly ILog _log = Logger.New();
    private static readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();

    private static readonly string _authenticationSecret = Guid.NewGuid().ToString();
    private static readonly Dictionary<string, List<SecurityKey>> _cachedKeys = new();

    public static IEnumerable<SecurityKey> ValidateKey(string sessionId,
                                                       string tokenString,
                                                       string issuer,
                                                       string audience)
    {
        if (!_cachedKeys.TryGetValue(sessionId, out var ks))
        {
            ks = new List<SecurityKey> { GetKey(sessionId) };
        }

        return ValidateCurrentToken(tokenString, issuer, audience, ks[0]) ? ks : [];
    }

    public static bool ValidateCurrentToken(string token, string issuer, string audience, SecurityKey key)
    {
        try
        {
            _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = key
            }, out SecurityToken validatedToken);
        }
        catch
        {
            return false;
        }
        return true;
    }

    public static SymmetricSecurityKey GetKey(string sessionId)
    {
        return new SymmetricSecurityKey(GetSessionSecret(sessionId)) { KeyId = sessionId };
    }

    public static byte[] GetSessionSecret(string secretSalt)
    {
        var secret = CryptographyUtils.Encrypt(secretSalt, _authenticationSecret);
        return Encoding.UTF8.GetBytes(secret);
    }

    public static SecurityTokenDescriptor GetTokenDescriptor(Context context, string sessionId)
    {
        if (context.User == null) throw Exceptions.Invalid("User has to be set into context before hand.");

        var claims = new Claim[]
        {
            new(ClaimTypes.Name, context.User.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, context.User.Email),
            new(ClaimTypes.Role, "Superuser"), // TODO
            new(nameof(User.LoginSessionId), context.User.LoginSessionId),
            new(nameof(context.Environment), context.Environment.ToString()),
            new(nameof(context.Exchange), context.Exchange.ToString()),
        };
        return new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(GetKey(sessionId), SecurityAlgorithms.HmacSha256Signature),
            Issuer = "TradePort",
            Audience = "SpecialTradingUnicorn"
        };
    }
}
