namespace Kahoot.Backend.Application.GameSessions;

public sealed class PlayerAnswerRecord
{
    public required string SessionId { get; init; }
    public required Guid SelectedOptionId { get; init; }
    public required int ElapsedMilliseconds { get; init; }
}
