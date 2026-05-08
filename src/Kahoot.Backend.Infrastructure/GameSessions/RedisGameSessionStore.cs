using Kahoot.Backend.Application.GameSessions;
using StackExchange.Redis;

namespace Kahoot.Backend.Infrastructure.GameSessions;

internal sealed class RedisGameSessionStore(IConnectionMultiplexer redis) : IGameSessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(6);
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<bool> TryCreateSessionAsync(string gamePin, Guid quizId, CancellationToken cancellationToken = default)
    {
        var metaKey = GetSessionMetaKey(gamePin);
        if (await _database.KeyExistsAsync(metaKey))
        {
            return false;
        }

        await _database.HashSetAsync(metaKey, new[]
        {
            new HashEntry("status", "WaitingForPlayers"),
            new HashEntry("quizId", quizId.ToString()),
            new HashEntry("currentQuestionIndex", -1),
            new HashEntry("currentQuestionStartedAtUtc", string.Empty),
            new HashEntry("createdAtUtc", DateTime.UtcNow.ToString("O"))
        });
        await _database.KeyExpireAsync(metaKey, SessionTtl);
        return true;
    }

    public async Task<bool> SessionExistsAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(GetSessionMetaKey(gamePin));
    }

    public async Task<bool> TryAddPlayerAsync(string gamePin, string nickname, string sessionId, CancellationToken cancellationToken = default)
    {
        var metaKey = GetSessionMetaKey(gamePin);
        if (!await _database.KeyExistsAsync(metaKey))
        {
            return false;
        }

        var normalizedNickname = nickname.Trim().ToLowerInvariant();
        var nicknamesKey = GetSessionNicknamesKey(gamePin);
        var playersKey = GetSessionPlayersKey(gamePin);

        var nicknameAdded = await _database.SetAddAsync(nicknamesKey, normalizedNickname);
        if (!nicknameAdded)
        {
            return false;
        }

        var playerPayload = $"{nickname.Trim()}|{DateTime.UtcNow:O}";
        await _database.HashSetAsync(playersKey, sessionId, playerPayload);

        await _database.KeyExpireAsync(nicknamesKey, SessionTtl);
        await _database.KeyExpireAsync(playersKey, SessionTtl);

        return true;
    }

    public async Task<GameSessionMeta?> GetSessionMetaAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var metaKey = GetSessionMetaKey(gamePin);
        var values = await _database.HashGetAsync(metaKey, ["quizId", "currentQuestionIndex", "status", "currentQuestionStartedAtUtc"]);
        if (values[0].IsNullOrEmpty || values[1].IsNullOrEmpty)
        {
            return null;
        }

        if (!Guid.TryParse(values[0].ToString(), out var quizId))
        {
            return null;
        }

        if (!int.TryParse(values[1].ToString(), out var currentQuestionIndex))
        {
            return null;
        }

        return new GameSessionMeta
        {
            QuizId = quizId,
            CurrentQuestionIndex = currentQuestionIndex,
            Status = values[2].ToString() ?? "WaitingForPlayers",
            CurrentQuestionStartedAtUtc = DateTime.TryParse(values[3].ToString(), out var startedAtUtc)
                ? startedAtUtc
                : null
        };
    }

    public async Task SetCurrentQuestionIndexAsync(string gamePin, int questionIndex, CancellationToken cancellationToken = default)
    {
        await _database.HashSetAsync(GetSessionMetaKey(gamePin), "currentQuestionIndex", questionIndex);
    }

    public async Task SetCurrentQuestionStartedAtUtcAsync(string gamePin, DateTime startedAtUtc, CancellationToken cancellationToken = default)
    {
        await _database.HashSetAsync(
            GetSessionMetaKey(gamePin),
            "currentQuestionStartedAtUtc",
            startedAtUtc.ToString("O")
        );
    }

    public async Task SetStatusAsync(string gamePin, string status, CancellationToken cancellationToken = default)
    {
        await _database.HashSetAsync(GetSessionMetaKey(gamePin), "status", status);
    }

    public async Task SaveAnswerAsync(string gamePin, string sessionId, Guid questionId, Guid selectedOptionId, int elapsedMilliseconds, CancellationToken cancellationToken = default)
    {
        var key = GetQuestionAnswersKey(gamePin, questionId);
        var payload = $"{selectedOptionId:D}|{elapsedMilliseconds}";
        await _database.HashSetAsync(key, sessionId, payload);
        await _database.KeyExpireAsync(key, SessionTtl);
    }

    public async Task<bool> HasAnsweredAsync(string gamePin, string sessionId, Guid questionId, CancellationToken cancellationToken = default)
    {
        return await _database.HashExistsAsync(GetQuestionAnswersKey(gamePin, questionId), sessionId);
    }

    public async Task<IReadOnlyList<PlayerAnswerRecord>> GetAnswersAsync(string gamePin, Guid questionId, CancellationToken cancellationToken = default)
    {
        var entries = await _database.HashGetAllAsync(GetQuestionAnswersKey(gamePin, questionId));
        var result = new List<PlayerAnswerRecord>(entries.Length);

        foreach (var entry in entries)
        {
            var parts = entry.Value.ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (!Guid.TryParse(parts[0], out var selectedOptionId))
            {
                continue;
            }

            if (!int.TryParse(parts[1], out var elapsed))
            {
                continue;
            }

            result.Add(new PlayerAnswerRecord
            {
                SessionId = entry.Name.ToString(),
                SelectedOptionId = selectedOptionId,
                ElapsedMilliseconds = elapsed
            });
        }

        return result;
    }

    public async Task<string?> GetNicknameAsync(string gamePin, string sessionId, CancellationToken cancellationToken = default)
    {
        var value = await _database.HashGetAsync(GetSessionPlayersKey(gamePin), sessionId);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        var parts = value.ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    public async Task AddScoreAsync(string gamePin, string sessionId, string nickname, double score, CancellationToken cancellationToken = default)
    {
        var leaderboardKey = GetLeaderboardKey(gamePin);
        await _database.SortedSetIncrementAsync(leaderboardKey, sessionId, score);
        await _database.HashSetAsync(GetLeaderboardNicknameKey(gamePin), sessionId, nickname);
        await _database.KeyExpireAsync(leaderboardKey, SessionTtl);
        await _database.KeyExpireAsync(GetLeaderboardNicknameKey(gamePin), SessionTtl);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopLeaderboardAsync(string gamePin, int count, CancellationToken cancellationToken = default)
    {
        var leaderboardKey = GetLeaderboardKey(gamePin);
        var members = await _database.SortedSetRangeByRankWithScoresAsync(leaderboardKey, 0, count - 1, Order.Descending);
        var nicknameMap = await _database.HashGetAllAsync(GetLeaderboardNicknameKey(gamePin));
        var nicknameBySession = nicknameMap.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

        return members.Select(member => new LeaderboardEntry
        {
            SessionId = member.Element.ToString(),
            Nickname = nicknameBySession.GetValueOrDefault(member.Element.ToString(), member.Element.ToString()),
            TotalScore = member.Score,
            RoundBasePoints = 0,
            RoundBonusPoints = 0,
            RoundTotalPoints = 0
        }).ToList();
    }

    public async Task<int> GetPlayerCountAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        return (int)await _database.HashLengthAsync(GetSessionPlayersKey(gamePin));
    }

    public async Task<IReadOnlyList<string>> GetPlayersAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var entries = await _database.HashGetAllAsync(GetSessionPlayersKey(gamePin));
        var players = new List<(string Nickname, DateTime JoinedAtUtc)>(entries.Length);

        foreach (var entry in entries)
        {
            var parts = entry.Value.ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var joinedAtUtc = DateTime.MinValue;
            if (parts.Length > 1 && DateTime.TryParse(parts[1], out var parsed))
            {
                joinedAtUtc = parsed;
            }

            players.Add((parts[0], joinedAtUtc));
        }

        return players
            .OrderBy(x => x.JoinedAtUtc)
            .Select(x => x.Nickname)
            .ToList();
    }

    public async Task<bool> IsPlayerBannedAsync(string gamePin, string nickname, CancellationToken cancellationToken = default)
    {
        var normalizedNickname = nickname.Trim().ToLowerInvariant();
        return await _database.SetContainsAsync(GetBannedNicknamesKey(gamePin), normalizedNickname);
    }

    public async Task<bool> BanPlayerAsync(string gamePin, string nickname, CancellationToken cancellationToken = default)
    {
        var normalizedNickname = nickname.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedNickname))
        {
            return false;
        }

        var playersKey = GetSessionPlayersKey(gamePin);
        var nicknamesKey = GetSessionNicknamesKey(gamePin);
        var bannedKey = GetBannedNicknamesKey(gamePin);

        var entries = await _database.HashGetAllAsync(playersKey);
        string? targetSessionId = null;

        foreach (var entry in entries)
        {
            var parts = entry.Value.ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0].Trim().ToLowerInvariant() == normalizedNickname)
            {
                targetSessionId = entry.Name.ToString();
                break;
            }
        }

        var removed = false;
        if (!string.IsNullOrWhiteSpace(targetSessionId))
        {
            removed = await _database.HashDeleteAsync(playersKey, targetSessionId);
            await _database.SetRemoveAsync(nicknamesKey, normalizedNickname);
            await _database.KeyExpireAsync(playersKey, SessionTtl);
            await _database.KeyExpireAsync(nicknamesKey, SessionTtl);
        }

        await _database.SetAddAsync(bannedKey, normalizedNickname);
        await _database.KeyExpireAsync(bannedKey, SessionTtl);

        return removed;
    }

    private static string GetSessionMetaKey(string gamePin) => $"game:{gamePin}:meta";
    private static string GetSessionNicknamesKey(string gamePin) => $"game:{gamePin}:nicknames";
    private static string GetSessionPlayersKey(string gamePin) => $"game:{gamePin}:players";
    private static string GetQuestionAnswersKey(string gamePin, Guid questionId) => $"game:{gamePin}:question:{questionId:D}:answers";
    private static string GetLeaderboardKey(string gamePin) => $"game:{gamePin}:leaderboard";
    private static string GetLeaderboardNicknameKey(string gamePin) => $"game:{gamePin}:leaderboard:nicknames";
    private static string GetBannedNicknamesKey(string gamePin) => $"game:{gamePin}:banned_nicknames";
}
