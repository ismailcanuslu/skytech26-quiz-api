using System.Text.RegularExpressions;
using System.Security.Claims;
using Kahoot.Backend.Api.Contracts.GameSessions;
using Kahoot.Backend.Api.Hubs;
using Kahoot.Backend.Api.Services;
using Kahoot.Backend.Application.Authentication;
using Kahoot.Backend.Application.GameSessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Kahoot.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class GameSessionController(IGamePinGenerator gamePinGenerator) : ControllerBase
{
    private static readonly Regex PinRegex = new("^[0-9]{6}$", RegexOptions.Compiled);

    [HttpPost("start")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartGameSessionRequest request,
        [FromServices] IGameSessionStore gameSessionStore,
        CancellationToken cancellationToken)
    {
        string gamePin;
        var created = false;

        for (var i = 0; i < 10; i++)
        {
            gamePin = gamePinGenerator.GenerateSixDigitPin();
            created = await gameSessionStore.TryCreateSessionAsync(gamePin, request.QuizId, cancellationToken);
            if (created)
            {
                return Ok(new { gamePin });
            }
        }

        if (!created)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Could not generate unique game PIN.");
        }

        gamePin = gamePinGenerator.GenerateSixDigitPin();
        return Ok(new { gamePin });
    }

    [HttpPost("{gamePin}/next-question")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> NextQuestion(
        [FromRoute] string gamePin,
        [FromServices] IGameFlowService gameFlowService,
        [FromServices] IHubContext<GameHub> hubContext,
        [FromServices] IGameRoundScheduler roundScheduler,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var question = await gameFlowService.ActivateNextQuestionAsync(gamePin, cancellationToken);
        if (question is null)
        {
            return NotFound("Next question not found for this game session.");
        }

        var payload = new NextQuestionEvent
        {
            GamePin = question.GamePin,
            QuestionId = question.QuestionId,
            QuestionIndex = question.QuestionIndex,
            TotalQuestions = question.TotalQuestions,
            Text = question.Text,
            TimeLimit = question.TimeLimit,
            Points = question.Points,
            StartedAtUtc = question.StartedAtUtc,
            Options = question.Options.Select(x => new NextQuestionOptionEvent
            {
                Id = x.Id,
                Text = x.Text
            }).ToList()
        };

        await hubContext.Clients
            .Group(GameHub.GetGroupName(gamePin))
            .SendAsync("NextQuestion", payload, cancellationToken);

        roundScheduler.Schedule(gamePin, question.QuestionIndex, question.TimeLimit);

        return Ok(payload);
    }

    [HttpPost("{gamePin}/show-correct-answer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ShowCorrectAnswer(
        [FromRoute] string gamePin,
        [FromServices] IGameFlowService gameFlowService,
        [FromServices] IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var result = await gameFlowService.GetCurrentCorrectAnswerAsync(gamePin, cancellationToken);
        if (result is null)
        {
            return NotFound("Current question or correct answer not found.");
        }

        var payload = new ShowCorrectAnswerEvent
        {
            GamePin = result.GamePin,
            QuestionId = result.QuestionId,
            CorrectOptionId = result.CorrectOptionId,
            CorrectOptionText = result.CorrectOptionText
        };

        await hubContext.Clients
            .Group(GameHub.GetGroupName(gamePin))
            .SendAsync("ShowCorrectAnswer", payload, cancellationToken);

        return Ok(payload);
    }

    [HttpPost("{gamePin}/submit-answer")]
    [Authorize(Roles = "Player")]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] string gamePin,
        [FromBody] SubmitAnswerRequest request,
        [FromServices] IGameFlowService gameFlowService,
        [FromServices] IGameSessionStore gameSessionStore,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var sessionId = User.FindFirstValue("sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Unauthorized();
        }

        var result = await gameFlowService.SubmitAnswerAsync(
            gamePin,
            sessionId,
            request.SelectedOptionId,
            request.ElapsedMilliseconds,
            cancellationToken);

        var nickname = User.FindFirstValue("nickname");
        if (!string.IsNullOrWhiteSpace(nickname))
        {
            var banned = await gameSessionStore.IsPlayerBannedAsync(gamePin, nickname, cancellationToken);
            if (banned)
            {
                return Forbid();
            }
        }

        if (!result.Accepted)
        {
            return BadRequest(result.Reason);
        }

        return Ok(new { accepted = true });
    }

    [HttpPost("{gamePin}/show-leaderboard")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ShowLeaderboard(
        [FromRoute] string gamePin,
        [FromServices] IGameFlowService gameFlowService,
        [FromServices] IGameSessionStore gameSessionStore,
        [FromServices] IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var leaderboard = await gameFlowService.BuildLeaderboardAsync(gamePin, cancellationToken);
        if (leaderboard is null)
        {
            return NotFound("Leaderboard could not be calculated.");
        }

        var payload = new ShowLeaderboardEvent
        {
            GamePin = gamePin,
            RoundIndex = (await gameSessionStore.GetSessionMetaAsync(gamePin, cancellationToken))?.CurrentQuestionIndex ?? 0,
            Top10 = leaderboard.Select(x => new LeaderboardItem
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
            .Group(GameHub.GetAdminGroupName(gamePin))
            .SendAsync("ShowLeaderboard", payload, cancellationToken);

        return Ok(payload);
    }

    [HttpPost("{gamePin}/end")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EndGame(
        [FromRoute] string gamePin,
        [FromServices] IGameFlowService gameFlowService,
        [FromServices] IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var finalTop5 = await gameFlowService.EndGameAsync(gamePin, cancellationToken);
        if (finalTop5 is null)
        {
            return NotFound("Game session not found.");
        }

        var payload = new EndGameEvent
        {
            GamePin = gamePin,
            Status = "Finished",
            FinalTop10 = finalTop5.Select(x => new LeaderboardItem
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
            .SendAsync("EndGame", payload, cancellationToken);

        return Ok(payload);
    }

    [HttpPost("join")]
    [AllowAnonymous]
    [ProducesResponseType<JoinGameResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(
        [FromBody] JoinGameRequest request,
        [FromServices] IGameSessionStore gameSessionStore,
        [FromServices] IPlayerAuthService playerAuthService,
        [FromServices] IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(request.GamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var nickname = request.Nickname.Trim();
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return BadRequest("Nickname is required.");
        }

        var sessionExists = await gameSessionStore.SessionExistsAsync(request.GamePin, cancellationToken);
        if (!sessionExists)
        {
            return NotFound("Game session not found.");
        }

        var banned = await gameSessionStore.IsPlayerBannedAsync(request.GamePin, nickname, cancellationToken);
        if (banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You are banned from this game session.");
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var added = await gameSessionStore.TryAddPlayerAsync(request.GamePin, nickname, sessionId, cancellationToken);
        if (!added)
        {
            return Conflict("Nickname already exists in this game session.");
        }

        var token = playerAuthService.CreateToken(nickname, sessionId);
        var joinedAtUtc = DateTime.UtcNow;
        var playerCount = await gameSessionStore.GetPlayerCountAsync(request.GamePin, cancellationToken);

        await hubContext.Clients
            .Group(GameHub.GetGroupName(request.GamePin))
            .SendAsync("PlayerJoined", new PlayerJoinedEvent
            {
                GamePin = request.GamePin,
                Nickname = nickname,
                SessionId = sessionId,
                PlayerCount = playerCount,
                JoinedAtUtc = joinedAtUtc
            }, cancellationToken);

        return Ok(new JoinGameResponse
        {
            GamePin = request.GamePin,
            Nickname = nickname,
            SessionId = sessionId,
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc
        });
    }

    [HttpPost("{gamePin}/ban-player")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BanPlayer(
        [FromRoute] string gamePin,
        [FromBody] BanPlayerRequest request,
        [FromServices] IGameSessionStore gameSessionStore,
        [FromServices] IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (!PinRegex.IsMatch(gamePin))
        {
            return BadRequest("Game PIN must be exactly 6 digits.");
        }

        var nickname = request.Nickname.Trim();
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return BadRequest("Nickname is required.");
        }

        var sessionExists = await gameSessionStore.SessionExistsAsync(gamePin, cancellationToken);
        if (!sessionExists)
        {
            return NotFound("Game session not found.");
        }

        var removed = await gameSessionStore.BanPlayerAsync(gamePin, nickname, cancellationToken);
        if (!removed)
        {
            return NotFound("Player not found.");
        }

        var playerCount = await gameSessionStore.GetPlayerCountAsync(gamePin, cancellationToken);
        await hubContext.Clients
            .Group(GameHub.GetGroupName(gamePin))
            .SendAsync("PlayerRemoved", new PlayerRemovedEvent
            {
                GamePin = gamePin,
                Nickname = nickname,
                PlayerCount = playerCount
            }, cancellationToken);

        return NoContent();
    }
}
