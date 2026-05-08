namespace Kahoot.Backend.Application.GameSessions;

public sealed class SubmitAnswerResult
{
    public required bool Accepted { get; init; }
    public required string Reason { get; init; }
}
