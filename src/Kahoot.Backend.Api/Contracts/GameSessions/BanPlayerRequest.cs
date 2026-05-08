namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class BanPlayerRequest
{
    public required string Nickname { get; init; }
}

public sealed class PlayerRemovedEvent
{
    public required string GamePin { get; init; }
    public required string Nickname { get; init; }
    public required int PlayerCount { get; init; }
}
