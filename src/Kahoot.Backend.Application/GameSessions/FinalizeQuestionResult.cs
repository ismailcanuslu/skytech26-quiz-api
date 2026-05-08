namespace Kahoot.Backend.Application.GameSessions;

public sealed class FinalizeQuestionResult
{
    public required CorrectAnswerResult CorrectAnswer { get; init; }
    public required IReadOnlyList<LeaderboardEntry> Top10 { get; init; }
    public required bool IsGameFinished { get; init; }
}
