using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kahoot.Backend.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Backend.Infrastructure.Authentication;

internal sealed class PlayerAuthService(IConfiguration configuration) : IPlayerAuthService
{
    public LoginResult CreateToken(string nickname, string sessionId)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
        var issuer = jwtSection["Issuer"] ?? "Kahoot.Backend";
        var audience = jwtSection["Audience"] ?? "Kahoot.Client";
        var expiresMinutes = int.TryParse(jwtSection["GuestAccessTokenExpirationMinutes"], out var value) ? value : 15;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "Player"),
            new Claim("nickname", nickname),
            new Claim("sessionId", sessionId)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResult
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt
        };
    }
}
