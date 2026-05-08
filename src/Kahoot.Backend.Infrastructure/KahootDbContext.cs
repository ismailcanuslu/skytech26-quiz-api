using Microsoft.EntityFrameworkCore;
using Kahoot.Backend.Domain.Entities;

namespace Kahoot.Backend.Infrastructure;

public sealed class KahootDbContext(DbContextOptions<KahootDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KahootDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
