using Kahoot.Backend.Api.Contracts.Auth;
using Kahoot.Backend.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    [HttpPost("admin/login")]
    [AllowAnonymous]
    [ProducesResponseType<AdminLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await adminAuthService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(new AdminLoginResponse
        {
            AccessToken = result.AccessToken,
            ExpiresAtUtc = result.ExpiresAtUtc
        });
    }

    [HttpGet("admin/me")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetCurrentAdmin()
    {
        return Ok(new
        {
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }

    [HttpPost("player/token")]
    [AllowAnonymous]
    [ProducesResponseType<AdminLoginResponse>(StatusCodes.Status200OK)]
    public IActionResult CreatePlayerToken(
        [FromBody] PlayerLoginRequest request,
        [FromServices] IPlayerAuthService playerAuthService)
    {
        var result = playerAuthService.CreateToken(request.Nickname, request.SessionId);

        return Ok(new AdminLoginResponse
        {
            AccessToken = result.AccessToken,
            ExpiresAtUtc = result.ExpiresAtUtc
        });
    }
}
