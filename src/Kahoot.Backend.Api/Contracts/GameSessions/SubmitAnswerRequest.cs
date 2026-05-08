namespace Kahoot.Backend.Api.Contracts.GameSessions;

public sealed class SubmitAnswerRequest
{
    public required Guid SelectedOptionId { get; init; }
    public required int ElapsedMilliseconds { get; init; }
}
