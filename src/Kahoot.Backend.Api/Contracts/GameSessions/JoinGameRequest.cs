namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class JoinGameRequest
{
    public required string GamePin { get; init; }
    public required string Nickname { get; init; }
}
