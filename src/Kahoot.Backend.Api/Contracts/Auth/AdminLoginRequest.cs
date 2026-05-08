namespace Kahoot.Backend.Api.Contracts.Auth;

public sealed class AdminLoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
