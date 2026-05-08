namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class ShowCorrectAnswerEvent
{
    public required string GamePin { get; init; }
    public required Guid QuestionId { get; init; }
    public required Guid CorrectOptionId { get; init; }
    public required string CorrectOptionText { get; init; }
}
