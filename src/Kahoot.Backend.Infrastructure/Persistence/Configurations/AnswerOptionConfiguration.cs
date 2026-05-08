using Kahoot.Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Backend.Infrastructure.Persistence.Configurations;

public sealed class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.IsCorrect)
            .IsRequired();

        builder.HasOne(x => x.Question)
            .WithMany(x => x.AnswerOptions)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
