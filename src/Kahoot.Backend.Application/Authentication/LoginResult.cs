namespace Kahoot.Backend.Application.Authentication;

public sealed class LoginResult
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
