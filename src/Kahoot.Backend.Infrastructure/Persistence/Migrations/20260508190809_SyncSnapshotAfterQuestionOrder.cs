using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncSnapshotAfterQuestionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_QuizId",
                table: "Questions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuizId",
                table: "Questions",
                column: "QuizId");
        }
    }
}
