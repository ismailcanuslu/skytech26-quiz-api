namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class PlayerJoinedEvent
{
    public required string GamePin { get; init; }
    public required string Nickname { get; init; }
    public required string SessionId { get; init; }
    public required int PlayerCount { get; init; }
    public required DateTime JoinedAtUtc { get; init; }
}
