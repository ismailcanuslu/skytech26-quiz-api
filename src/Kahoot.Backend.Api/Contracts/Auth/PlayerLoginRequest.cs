namespace Kahoot.Backend.Api.Contracts.Auth;

public sealed class PlayerLoginRequest
{
    public required string Nickname { get; init; }
    public required string SessionId { get; init; }
}
