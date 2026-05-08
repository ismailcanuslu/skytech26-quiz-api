namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class JoinGameResponse
{
    public required string GamePin { get; init; }
    public required string Nickname { get; init; }
    public required string SessionId { get; init; }
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
