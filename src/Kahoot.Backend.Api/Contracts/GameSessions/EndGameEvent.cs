namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class EndGameEvent
{
    public required string GamePin { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<LeaderboardItem> FinalTop10 { get; init; }
}
