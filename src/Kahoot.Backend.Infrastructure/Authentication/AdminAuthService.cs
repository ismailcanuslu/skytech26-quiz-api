using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kahoot.Backend.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Backend.Infrastructure.Authentication;

internal sealed class AdminAuthService(KahootDbContext dbContext, IConfiguration configuration) : IAdminAuthService
{
    public async Task<LoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
        var issuer = jwtSection["Issuer"] ?? "Kahoot.Backend";
        var audience = jwtSection["Audience"] ?? "Kahoot.Client";
        var expiresMinutes = int.TryParse(jwtSection["AccessTokenExpirationMinutes"], out var value) ? value : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
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
