using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deliberately empty Up/Down — EF's migration diff sees a new "xmin" shadow property on
    /// Submission (SubmissionConfiguration's IsRowVersion() concurrency-token config) and by
    /// default wants to generate an AddColumn for it, but xmin is one of Postgres's built-in
    /// system columns present on every table already — there is nothing to add or drop, and
    /// running the generated AddColumn against a real column named "xmin" would fail outright
    /// (Postgres reserves that name). This migration exists only so __EFMigrationsHistory records
    /// the model change and AppDbContextModelSnapshot.cs stays in sync for future `migrations add`
    /// diffing — the Up()/Down() bodies are intentionally no-ops. Safe to run
    /// `dotnet ef database update` for this one; it does nothing to the real schema.
    /// </summary>
    public partial class AddSubmissionXminConcurrencyToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
