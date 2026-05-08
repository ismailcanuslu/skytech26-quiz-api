using Kahoot.Backend.Application.GameSessions;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Backend.Infrastructure.GameSessions;

internal sealed class GameFlowService(KahootDbContext dbContext, IGameSessionStore gameSessionStore) : IGameFlowService
{
    public async Task<NextQuestionResult?> ActivateNextQuestionAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);
        if (meta is null || meta.Status is "QuestionActive" or "Finished")
        {
            return null;
        }

        var nextIndex = meta.CurrentQuestionIndex + 1;
        var questions = await dbContext.Questions
            .AsNoTracking()
            .Where(x => x.QuizId == meta.QuizId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Text,
                x.TimeLimit,
                x.Points,
                Options = x.AnswerOptions
                    .OrderBy(o => o.Id)
                    .Select(o => new { o.Id, o.Text })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var question = questions.Skip(nextIndex).FirstOrDefault();

        if (question is null)
        {
            return null;
        }

        await gameSessionStore.SetCurrentQuestionIndexAsync(gamePin, nextIndex, cancellationToken);
        var startedAtUtc = DateTime.UtcNow;
        await gameSessionStore.SetCurrentQuestionStartedAtUtcAsync(gamePin, startedAtUtc, cancellationToken);
        await gameSessionStore.SetStatusAsync(gamePin, "QuestionActive", cancellationToken);

        return new NextQuestionResult
        {
            GamePin = gamePin,
            QuestionId = question.Id,
            QuestionIndex = nextIndex,
            TotalQuestions = questions.Count,
            Text = question.Text,
            TimeLimit = question.TimeLimit,
            Points = question.Points,
            StartedAtUtc = startedAtUtc,
            Options = question.Options.Select(x => new QuestionOptionResult
            {
                Id = x.Id,
                Text = x.Text
            }).ToList()
        };
    }

    public async Task<CorrectAnswerResult?> GetCurrentCorrectAnswerAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);
        if (meta is null || meta.CurrentQuestionIndex < 0)
        {
            return null;
        }

        var currentQuestion = await dbContext.Questions
            .AsNoTracking()
            .Where(x => x.QuizId == meta.QuizId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .Skip(meta.CurrentQuestionIndex)
            .Take(1)
            .Select(x => new
            {
                x.Id,
                Correct = x.AnswerOptions
                    .Where(o => o.IsCorrect)
                    .Select(o => new { o.Id, o.Text })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentQuestion?.Correct is null)
        {
            return null;
        }

        await gameSessionStore.SetStatusAsync(gamePin, "QuestionResult", cancellationToken);

        return new CorrectAnswerResult
        {
            GamePin = gamePin,
            QuestionId = currentQuestion.Id,
            CorrectOptionId = currentQuestion.Correct.Id,
            CorrectOptionText = currentQuestion.Correct.Text
        };
    }

    public async Task<SubmitAnswerResult> SubmitAnswerAsync(
        string gamePin,
        string sessionId,
        Guid selectedOptionId,
        int elapsedMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);
        if (meta is null || meta.CurrentQuestionIndex < 0 || meta.Status != "QuestionActive")
        {
            return new SubmitAnswerResult { Accepted = false, Reason = "Question is not active." };
        }

        var currentQuestion = await dbContext.Questions
            .AsNoTracking()
            .Where(x => x.QuizId == meta.QuizId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .Skip(meta.CurrentQuestionIndex)
            .Take(1)
            .Select(x => new
            {
                x.Id,
                x.TimeLimit,
                OptionIds = x.AnswerOptions.Select(o => o.Id).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentQuestion is null || !currentQuestion.OptionIds.Contains(selectedOptionId))
        {
            return new SubmitAnswerResult { Accepted = false, Reason = "Invalid answer option." };
        }

        var alreadyAnswered = await gameSessionStore.HasAnsweredAsync(gamePin, sessionId, currentQuestion.Id, cancellationToken);
        if (alreadyAnswered)
        {
            return new SubmitAnswerResult { Accepted = false, Reason = "Answer already submitted." };
        }

        var safeElapsed = Math.Max(0, elapsedMilliseconds);
        var maxElapsed = currentQuestion.TimeLimit * 1000;
        var boundedElapsed = Math.Min(safeElapsed, maxElapsed);

        await gameSessionStore.SaveAnswerAsync(gamePin, sessionId, currentQuestion.Id, selectedOptionId, boundedElapsed, cancellationToken);
        return new SubmitAnswerResult { Accepted = true, Reason = "Accepted" };
    }

    public async Task<IReadOnlyList<LeaderboardEntry>?> BuildLeaderboardAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var finalized = await FinalizeCurrentQuestionAsync(gamePin, cancellationToken);
        return finalized?.Top10;
    }

    public async Task<IReadOnlyList<LeaderboardEntry>?> EndGameAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);
        if (meta is null)
        {
            return null;
        }

        await gameSessionStore.SetStatusAsync(gamePin, "Finished", cancellationToken);
        return await gameSessionStore.GetTopLeaderboardAsync(gamePin, 10, cancellationToken);
    }

    public async Task<FinalizeQuestionResult?> FinalizeCurrentQuestionAsync(string gamePin, CancellationToken cancellationToken = default)
    {
        var meta = await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken);
        if (meta is null || meta.CurrentQuestionIndex < 0)
        {
            return null;
        }

        var questions = await dbContext.Questions
            .AsNoTracking()
            .Where(x => x.QuizId == meta.QuizId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Points,
                x.TimeLimit,
                Correct = x.AnswerOptions.Where(o => o.IsCorrect).Select(o => new { o.Id, o.Text }).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        if (meta.CurrentQuestionIndex >= questions.Count)
        {
            return null;
        }

        var question = questions[meta.CurrentQuestionIndex];
        if (question.Correct is null)
        {
            return null;
        }

        var answers = await gameSessionStore.GetAnswersAsync(gamePin, question.Id, cancellationToken);
        var correctAnswers = answers.Where(a => a.SelectedOptionId == question.Correct.Id).ToList();
        var totalPlayers = Math.Max(1, await gameSessionStore.GetPlayerCountAsync(gamePin, cancellationToken));
        var correctCount = correctAnswers.Count;

        var roundBySession = new Dictionary<string, (int BasePoints, int BonusPoints, int RoundTotal)>();
        foreach (var answer in correctAnswers)
        {
            var totalMs = Math.Max(1, question.TimeLimit * 1000);
            var remainingMs = Math.Max(0, totalMs - answer.ElapsedMilliseconds);
            var ratio = remainingMs / (double)totalMs;
            var basePoints = (int)Math.Round(300 + (699 * ratio), MidpointRounding.AwayFromZero);
            basePoints = Math.Clamp(basePoints, 300, 999);

            var rarityFactor = 1d - (correctCount / (double)totalPlayers);
            var bonusPoints = (int)Math.Round(999 * Math.Clamp(rarityFactor, 0d, 1d), MidpointRounding.AwayFromZero);
            bonusPoints = Math.Clamp(bonusPoints, 0, 999);

            var roundTotal = basePoints + bonusPoints;
            roundBySession[answer.SessionId] = (basePoints, bonusPoints, roundTotal);

            var nickname = await gameSessionStore.GetNicknameAsync(gamePin, answer.SessionId, cancellationToken) ?? answer.SessionId;
            await gameSessionStore.AddScoreAsync(gamePin, answer.SessionId, nickname, roundTotal, cancellationToken);
        }

        var isLastQuestion = meta.CurrentQuestionIndex >= questions.Count - 1;
        await gameSessionStore.SetStatusAsync(gamePin, isLastQuestion ? "Finished" : "Leaderboard", cancellationToken);

        var top10 = await gameSessionStore.GetTopLeaderboardAsync(gamePin, 10, cancellationToken);
        var mappedTop10 = top10.Select(entry =>
        {
            var round = roundBySession.GetValueOrDefault(entry.SessionId);
            return new LeaderboardEntry
            {
                SessionId = entry.SessionId,
                Nickname = entry.Nickname,
                TotalScore = entry.TotalScore,
                RoundBasePoints = round.BasePoints,
                RoundBonusPoints = round.BonusPoints,
                RoundTotalPoints = round.RoundTotal
            };
        }).ToList();

        return new FinalizeQuestionResult
        {
            CorrectAnswer = new CorrectAnswerResult
            {
                GamePin = gamePin,
                QuestionId = question.Id,
                CorrectOptionId = question.Correct.Id,
                CorrectOptionText = question.Correct.Text
            },
            Top10 = mappedTop10,
            IsGameFinished = isLastQuestion
        };
    }
}
