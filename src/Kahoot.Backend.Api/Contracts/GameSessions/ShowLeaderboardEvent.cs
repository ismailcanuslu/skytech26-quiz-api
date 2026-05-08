namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class ShowLeaderboardEvent
{
    public required string GamePin { get; init; }
    public required int RoundIndex { get; init; }
    public required IReadOnlyList<LeaderboardItem> Top10 { get; init; }
}

public sealed class LeaderboardItem
{
    public required string SessionId { get; init; }
    public required string Nickname { get; init; }
    public required double TotalScore { get; init; }
    public required int RoundBasePoints { get; init; }
    public required int RoundBonusPoints { get; init; }
    public required int RoundTotalPoints { get; init; }
}
