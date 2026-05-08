namespace Kahoot.Backend.Application.GameSessions;

public interface IGameFlowService
{
    Task<NextQuestionResult?> ActivateNextQuestionAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<CorrectAnswerResult?> GetCurrentCorrectAnswerAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<SubmitAnswerResult> SubmitAnswerAsync(string gamePin, string sessionId, Guid selectedOptionId, int elapsedMilliseconds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardEntry>?> BuildLeaderboardAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardEntry>?> EndGameAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<FinalizeQuestionResult?> FinalizeCurrentQuestionAsync(string gamePin, CancellationToken cancellationToken = default);
}
