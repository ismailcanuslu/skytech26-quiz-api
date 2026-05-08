namespace Kahoot.Backend.Application.GameSessions;

public sealed class GameSessionMeta
{
    public required Guid QuizId { get; init; }
    public required int CurrentQuestionIndex { get; init; }
    public required string Status { get; init; }
    public DateTime? CurrentQuestionStartedAtUtc { get; init; }
}
