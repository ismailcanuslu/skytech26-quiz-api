namespace Kahoot.Backend.Application.GameSessions;

public sealed class CorrectAnswerResult
{
    public required string GamePin { get; init; }
    public required Guid QuestionId { get; init; }
    public required Guid CorrectOptionId { get; init; }
    public required string CorrectOptionText { get; init; }
}
