namespace Kahoot.Backend.Api.Contracts.Auth;

public sealed class AdminLoginResponse
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
