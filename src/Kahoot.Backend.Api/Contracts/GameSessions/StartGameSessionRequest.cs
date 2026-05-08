namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class StartGameSessionRequest
{
    public required Guid QuizId { get; init; }
}
