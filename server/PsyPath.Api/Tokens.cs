using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PsyPath.Api;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "psypath";
    public int Days { get; set; } = 30;
}

public class Tokens(JwtOptions options)
{
    readonly JsonWebTokenHandler handler = new();
    readonly SigningCredentials credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);

    public string Create(long userId, string role) => handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = options.Issuer,
        Audience = options.Issuer,
        Expires = DateTime.UtcNow.AddDays(options.Days),
        Claims = new Dictionary<string, object> { ["sub"] = userId.ToString(), ["role"] = role },
        SigningCredentials = credentials
    });
}

public static class ClaimsExtensions
{
    public static long UserId(this ClaimsPrincipal user) => long.Parse(user.FindFirstValue("sub")!);
}
