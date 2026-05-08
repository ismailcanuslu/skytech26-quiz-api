using Kahoot.Backend.Api.Contracts.Quizzes;
using Kahoot.Backend.Domain.Entities;
using Kahoot.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/quizzes")]
[Authorize(Roles = "Admin")]
public sealed class AdminQuizzesController(KahootDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuizSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var quizzes = await dbContext.Quizzes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new QuizSummaryResponse
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                IsActive = x.IsActive,
                QuestionCount = x.Questions.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(quizzes);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<QuizDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var quiz = await dbContext.Quizzes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new QuizDetailResponse
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                IsActive = x.IsActive,
                Questions = x.Questions
                    .OrderBy(q => q.Order)
                    .ThenBy(q => q.Id)
                    .Select(q => new QuestionResponse
                    {
                        Id = q.Id,
                        Order = q.Order,
                        Text = q.Text,
                        TimeLimit = q.TimeLimit,
                        Points = q.Points,
                        AnswerOptions = q.AnswerOptions
                            .OrderBy(o => o.Id)
                            .Select(o => new AnswerOptionResponse
                            {
                                Id = o.Id,
                                Text = o.Text,
                                IsCorrect = o.IsCorrect
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return NotFound();
        }

        return Ok(quiz);
    }

    [HttpPost]
    [ProducesResponseType<QuizDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateQuizRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Quiz title is required.");
        }

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        dbContext.Quizzes.Add(quiz);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, new QuizDetailResponse
        {
            Id = quiz.Id,
            Title = quiz.Title,
            Description = quiz.Description,
            CreatedAt = quiz.CreatedAt,
            IsActive = quiz.IsActive,
            Questions = []
        });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<QuizDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateQuizRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Quiz title is required.");
        }

        var quiz = await dbContext.Quizzes
            .Include(x => x.Questions)
            .ThenInclude(x => x.AnswerOptions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.Title = request.Title.Trim();
        quiz.Description = request.Description?.Trim() ?? string.Empty;
        quiz.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapQuizDetail(quiz));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var quiz = await dbContext.Quizzes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        quiz.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/questions")]
    [ProducesResponseType<QuestionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddQuestion([FromRoute] Guid id, [FromBody] CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var quiz = await dbContext.Quizzes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        var validation = ValidateQuestionPayload(request.AnswerOptions.Select(x => x.IsCorrect).ToList(), request.TimeLimit, request.Points, request.Text);
        if (validation is not null)
        {
            return BadRequest(validation);
        }

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = id,
            Order = (await dbContext.Questions
                .Where(x => x.QuizId == id)
                .MaxAsync(x => (int?)x.Order, cancellationToken) ?? -1) + 1,
            Text = request.Text.Trim(),
            TimeLimit = request.TimeLimit,
            Points = request.Points,
            AnswerOptions = request.AnswerOptions.Select(x => new AnswerOption
            {
                Id = Guid.NewGuid(),
                Text = x.Text.Trim(),
                IsCorrect = x.IsCorrect
            }).ToList()
        };

        dbContext.Questions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new QuestionResponse
        {
            Id = question.Id,
            Order = question.Order,
            Text = question.Text,
            TimeLimit = question.TimeLimit,
            Points = question.Points,
            AnswerOptions = question.AnswerOptions.Select(x => new AnswerOptionResponse
            {
                Id = x.Id,
                Text = x.Text,
                IsCorrect = x.IsCorrect
            }).ToList()
        });
    }

    [HttpPut("{quizId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType<QuestionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuestion(
        [FromRoute] Guid quizId,
        [FromRoute] Guid questionId,
        [FromBody] UpdateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var question = await dbContext.Questions
            .Include(x => x.AnswerOptions)
            .FirstOrDefaultAsync(x => x.Id == questionId && x.QuizId == quizId, cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var validation = ValidateQuestionPayload(request.AnswerOptions.Select(x => x.IsCorrect).ToList(), request.TimeLimit, request.Points, request.Text);
        if (validation is not null)
        {
            return BadRequest(validation);
        }

        question.Text = request.Text.Trim();
        question.TimeLimit = request.TimeLimit;
        question.Points = request.Points;

        var incomingIds = request.AnswerOptions.Select(x => x.Id).ToHashSet();
        if (incomingIds.Count != request.AnswerOptions.Count || incomingIds.Count != question.AnswerOptions.Count)
        {
            return BadRequest("Answer option ids must match existing options exactly.");
        }

        var removable = question.AnswerOptions.Where(x => !incomingIds.Contains(x.Id)).ToList();
        if (removable.Count > 0)
        {
            return BadRequest("Answer option ids must match existing options exactly.");
        }

        foreach (var option in request.AnswerOptions)
        {
            var existing = question.AnswerOptions.FirstOrDefault(x => x.Id == option.Id);
            if (existing is null)
            {
                return BadRequest($"Answer option '{option.Id}' does not belong to question.");
            }

            existing.Text = option.Text.Trim();
            existing.IsCorrect = option.IsCorrect;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new QuestionResponse
        {
            Id = question.Id,
            Order = question.Order,
            Text = question.Text,
            TimeLimit = question.TimeLimit,
            Points = question.Points,
            AnswerOptions = question.AnswerOptions
                .OrderBy(x => x.Id)
                .Select(x => new AnswerOptionResponse
                {
                    Id = x.Id,
                    Text = x.Text,
                    IsCorrect = x.IsCorrect
                })
                .ToList()
        });
    }

    [HttpDelete("{quizId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuestion([FromRoute] Guid quizId, [FromRoute] Guid questionId, CancellationToken cancellationToken)
    {
        var question = await dbContext.Questions.FirstOrDefaultAsync(x => x.Id == questionId && x.QuizId == quizId, cancellationToken);
        if (question is null)
        {
            return NotFound();
        }

        dbContext.Questions.Remove(question);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/questions/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderQuestions(
        [FromRoute] Guid id,
        [FromBody] ReorderQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var quiz = await dbContext.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        var questions = await dbContext.Questions
            .Where(x => x.QuizId == id)
            .ToListAsync(cancellationToken);

        if (request.QuestionIds.Count != questions.Count)
        {
            return BadRequest("QuestionIds count must match quiz questions count.");
        }

        var existingIds = questions.Select(x => x.Id).ToHashSet();
        if (!request.QuestionIds.All(existingIds.Contains) || request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
        {
            return BadRequest("QuestionIds must include each quiz question exactly once.");
        }

        var orderById = request.QuestionIds
            .Select((questionId, index) => new { questionId, index })
            .ToDictionary(x => x.questionId, x => x.index);

        foreach (var question in questions)
        {
            question.Order = orderById[question.Id];
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? ValidateQuestionPayload(IReadOnlyCollection<bool> correctnessFlags, int timeLimit, int points, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Question text is required.";
        }

        if (timeLimit <= 0)
        {
            return "TimeLimit must be greater than 0.";
        }

        if (points <= 0)
        {
            return "Points must be greater than 0.";
        }

        if (correctnessFlags.Count < 2)
        {
            return "At least 2 answer options are required.";
        }

        if (correctnessFlags.Count(x => x) != 1)
        {
            return "Exactly one answer option must be correct.";
        }

        return null;
    }

    private static QuizDetailResponse MapQuizDetail(Quiz quiz)
    {
        return new QuizDetailResponse
        {
            Id = quiz.Id,
            Title = quiz.Title,
            Description = quiz.Description,
            CreatedAt = quiz.CreatedAt,
            IsActive = quiz.IsActive,
            Questions = quiz.Questions
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .Select(q => new QuestionResponse
                {
                    Id = q.Id,
                    Order = q.Order,
                    Text = q.Text,
                    TimeLimit = q.TimeLimit,
                    Points = q.Points,
                    AnswerOptions = q.AnswerOptions
                        .OrderBy(o => o.Id)
                        .Select(o => new AnswerOptionResponse
                        {
                            Id = o.Id,
                            Text = o.Text,
                            IsCorrect = o.IsCorrect
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
