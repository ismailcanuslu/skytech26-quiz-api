using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Kahoot.Backend.Infrastructure;

#nullable disable

namespace Kahoot.Backend.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(KahootDbContext))]
    [Migration("20260508190000_AddQuestionOrderAndReorderSupport")]
    public partial class AddQuestionOrderAndReorderSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "QuizId" ORDER BY "Id") - 1 AS row_num
                    FROM "Questions"
                )
                UPDATE "Questions" q
                SET "Order" = ordered.row_num
                FROM ordered
                WHERE q."Id" = ordered."Id";
            """);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuizId_Order",
                table: "Questions",
                columns: new[] { "QuizId", "Order" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_QuizId_Order",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Questions");
        }
    }
}
