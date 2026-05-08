namespace Kahoot.Backend.Application.GameSessions;

public sealed class LeaderboardEntry
{
    public required string SessionId { get; init; }
    public required string Nickname { get; init; }
    public required double TotalScore { get; init; }
    public int RoundBasePoints { get; init; }
    public int RoundBonusPoints { get; init; }
    public int RoundTotalPoints { get; init; }
}
