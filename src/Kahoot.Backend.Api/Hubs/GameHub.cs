using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Kahoot.Backend.Application.GameSessions;
using Kahoot.Backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Backend.Api.Hubs;

[Authorize]
public sealed class GameHub(
    IGameSessionStore gameSessionStore,
    KahootDbContext dbContext) : Hub
{
    public async Task JoinGameGroup(string gamePin)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(gamePin));

        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetAdminGroupName(gamePin));
        }
    }

    public async Task<LobbySnapshotEvent> GetLobbySnapshot(string gamePin)
    {
        var cancellationToken = Context.ConnectionAborted;
        var players = await gameSessionStore.GetPlayersAsync(gamePin, cancellationToken);
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);

        CurrentQuestionSnapshotEvent? currentQuestion = null;
        if (meta is not null && meta.Status == "QuestionActive" && meta.CurrentQuestionIndex >= 0)
        {
            var totalQuestions = await dbContext.Questions
                .AsNoTracking()
                .CountAsync(x => x.QuizId == meta.QuizId, cancellationToken);

            var question = await dbContext.Questions
                .AsNoTracking()
                .Where(x => x.QuizId == meta.QuizId)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .Skip(meta.CurrentQuestionIndex)
                .Take(1)
                .Select(x => new
                {
                    x.Id,
                    x.Text,
                    x.TimeLimit,
                    x.Points,
                    Options = x.AnswerOptions
                        .OrderBy(o => o.Id)
                        .Select(o => new QuestionOptionSnapshotEvent
                        {
                            Id = o.Id,
                            Text = o.Text
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (question is not null)
            {
                currentQuestion = new CurrentQuestionSnapshotEvent
                {
                    GamePin = gamePin,
                    QuestionId = question.Id,
                    QuestionIndex = meta.CurrentQuestionIndex,
                    TotalQuestions = totalQuestions,
                    Text = question.Text,
                    TimeLimit = question.TimeLimit,
                    Points = question.Points,
                    StartedAtUtc = meta.CurrentQuestionStartedAtUtc ?? DateTime.UtcNow,
                    Options = question.Options
                };
            }
        }

        return new LobbySnapshotEvent
        {
            GamePin = gamePin,
            Players = players,
            PlayerCount = players.Count,
            Status = meta?.Status ?? "WaitingForPlayers",
            CurrentQuestion = currentQuestion
        };
    }

    public static string GetGroupName(string gamePin) => $"game-{gamePin}";
    public static string GetAdminGroupName(string gamePin) => $"game-{gamePin}:admins";
}

public sealed class LobbySnapshotEvent
{
    public required string GamePin { get; init; }
    public required IReadOnlyList<string> Players { get; init; }
    public required int PlayerCount { get; init; }
    public required string Status { get; init; }
    public CurrentQuestionSnapshotEvent? CurrentQuestion { get; init; }
}

public sealed class CurrentQuestionSnapshotEvent
{
    public required string GamePin { get; init; }
    public required Guid QuestionId { get; init; }
    public required int QuestionIndex { get; init; }
    public required int TotalQuestions { get; init; }
    public required string Text { get; init; }
    public required int TimeLimit { get; init; }
    public required int Points { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required IReadOnlyList<QuestionOptionSnapshotEvent> Options { get; init; }
}

public sealed class QuestionOptionSnapshotEvent
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
}
