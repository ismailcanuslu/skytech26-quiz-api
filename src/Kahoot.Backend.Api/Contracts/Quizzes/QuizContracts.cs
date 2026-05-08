namespace Kahoot.Backend.Api.Contracts.Quizzes;

public sealed class CreateQuizRequest
{
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class UpdateQuizRequest
{
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed class QuizSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
    public required int QuestionCount { get; init; }
}

public sealed class QuizDetailResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }
    public required IReadOnlyList<QuestionResponse> Questions { get; init; }
}

public sealed class QuestionResponse
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public required int TimeLimit { get; init; }
    public required int Points { get; init; }
    public required IReadOnlyList<AnswerOptionResponse> AnswerOptions { get; init; }
}

public sealed class AnswerOptionResponse
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}

public sealed class CreateQuestionRequest
{
    public required string Text { get; init; }
    public required int TimeLimit { get; init; }
    public required int Points { get; init; }
    public required IReadOnlyList<CreateAnswerOptionRequest> AnswerOptions { get; init; }
}

public sealed class UpdateQuestionRequest
{
    public required string Text { get; init; }
    public required int TimeLimit { get; init; }
    public required int Points { get; init; }
    public required IReadOnlyList<UpdateAnswerOptionRequest> AnswerOptions { get; init; }
}

public sealed class CreateAnswerOptionRequest
{
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}

public sealed class UpdateAnswerOptionRequest
{
    public Guid? Id { get; init; }
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}
