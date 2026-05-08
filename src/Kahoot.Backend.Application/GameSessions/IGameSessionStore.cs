namespace Kahoot.Backend.Application.GameSessions;

public interface IGameSessionStore
{
    Task<bool> TryCreateSessionAsync(string gamePin, Guid quizId, CancellationToken cancellationToken = default);
    Task<bool> SessionExistsAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<bool> TryAddPlayerAsync(string gamePin, string nickname, string sessionId, CancellationToken cancellationToken = default);
    Task<GameSessionMeta?> GetSessionMetaAsync(string gamePin, CancellationToken cancellationToken = default);
    Task SetCurrentQuestionIndexAsync(string gamePin, int questionIndex, CancellationToken cancellationToken = default);
    Task SetCurrentQuestionStartedAtUtcAsync(string gamePin, DateTime startedAtUtc, CancellationToken cancellationToken = default);
    Task SetStatusAsync(string gamePin, string status, CancellationToken cancellationToken = default);
    Task SaveAnswerAsync(string gamePin, string sessionId, Guid questionId, Guid selectedOptionId, int elapsedMilliseconds, CancellationToken cancellationToken = default);
    Task<bool> HasAnsweredAsync(string gamePin, string sessionId, Guid questionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerAnswerRecord>> GetAnswersAsync(string gamePin, Guid questionId, CancellationToken cancellationToken = default);
    Task<string?> GetNicknameAsync(string gamePin, string sessionId, CancellationToken cancellationToken = default);
    Task AddScoreAsync(string gamePin, string sessionId, string nickname, double score, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetTopLeaderboardAsync(string gamePin, int count, CancellationToken cancellationToken = default);
    Task<int> GetPlayerCountAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPlayersAsync(string gamePin, CancellationToken cancellationToken = default);
    Task<bool> IsPlayerBannedAsync(string gamePin, string nickname, CancellationToken cancellationToken = default);
    Task<bool> BanPlayerAsync(string gamePin, string nickname, CancellationToken cancellationToken = default);
}
