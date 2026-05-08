namespace Kahoot.Backend.Domain.Entities;

public sealed class Question
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public int TimeLimit { get; set; }
    public int Points { get; set; }

    public Quiz Quiz { get; set; } = default!;
    public ICollection<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();
}
