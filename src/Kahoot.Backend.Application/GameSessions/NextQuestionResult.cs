namespace Kahoot.Backend.Application.GameSessions;

public sealed class NextQuestionResult
{
    public required string GamePin { get; init; }
    public required Guid QuestionId { get; init; }
    public required int QuestionIndex { get; init; }
    public required int TotalQuestions { get; init; }
    public required string Text { get; init; }
    public required int TimeLimit { get; init; }
    public required int Points { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required IReadOnlyList<QuestionOptionResult> Options { get; init; }
}

public sealed class QuestionOptionResult
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
}
