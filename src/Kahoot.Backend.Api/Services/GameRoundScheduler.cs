using System.Collections.Concurrent;
using Kahoot.Backend.Api.Contracts.GameSessions;
using Kahoot.Backend.Api.Hubs;
using Kahoot.Backend.Application.GameSessions;
using Microsoft.AspNetCore.SignalR;

namespace Kahoot.Backend.Api.Services;

public sealed class GameRoundScheduler(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameHub> hubContext,
    ILogger<GameRoundScheduler> logger) : IGameRoundScheduler
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobs = new();

    public void Schedule(string gamePin, int questionIndex, int timeLimitSeconds)
    {
        if (_jobs.TryRemove(gamePin, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        var cts = new CancellationTokenSource();
        _jobs[gamePin] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeLimitSeconds), cts.Token);

                await using var scope = scopeFactory.CreateAsyncScope();
                var flow = scope.ServiceProvider.GetRequiredService<IGameFlowService>();
                var finalized = await flow.FinalizeCurrentQuestionAsync(gamePin, cts.Token);
                if (finalized is null)
                {
                    return;
                }

                var correctPayload = new ShowCorrectAnswerEvent
                {
                    GamePin = finalized.CorrectAnswer.GamePin,
                    QuestionId = finalized.CorrectAnswer.QuestionId,
                    CorrectOptionId = finalized.CorrectAnswer.CorrectOptionId,
                    CorrectOptionText = finalized.CorrectAnswer.CorrectOptionText
                };

                await hubContext.Clients
                    .Group(GameHub.GetGroupName(gamePin))
                    .SendAsync("ShowCorrectAnswer", correctPayload, cts.Token);

                var leaderboardPayload = new ShowLeaderboardEvent
                {
                    GamePin = gamePin,
                    RoundIndex = questionIndex,
                    Top10 = finalized.Top10.Select(x => new LeaderboardItem
                    {
                        SessionId = x.SessionId,
                        Nickname = x.Nickname,
                        TotalScore = Math.Round(x.TotalScore, 2),
                        RoundBasePoints = x.RoundBasePoints,
                        RoundBonusPoints = x.RoundBonusPoints,
                        RoundTotalPoints = x.RoundTotalPoints
                    }).ToList()
                };

                await hubContext.Clients
                    .Group(GameHub.GetGroupName(gamePin))
                    .SendAsync("ShowLeaderboard", leaderboardPayload, cts.Token);

                if (finalized.IsGameFinished)
                {
                    var endPayload = new EndGameEvent
                    {
                        GamePin = gamePin,
                        Status = "Finished",
                        FinalTop10 = leaderboardPayload.Top10
                    };

                    await hubContext.Clients
                        .Group(GameHub.GetGroupName(gamePin))
                        .SendAsync("EndGame", endPayload, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore cancellations from rescheduling
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Round scheduler failed for game pin {GamePin}", gamePin);
            }
            finally
            {
                if (_jobs.TryRemove(gamePin, out var current))
                {
                    current.Dispose();
                }
            }
        }, cts.Token);
    }
}
