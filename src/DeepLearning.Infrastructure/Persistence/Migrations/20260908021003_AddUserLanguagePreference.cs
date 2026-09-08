using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One additive column on users: <c>language_preference</c>, which language the web UI renders
    /// in ("en" or "zh"). NOT NULL with a server default of "en", so every existing row is
    /// backfilled to English on apply and no application code has to cope with a null. This is a
    /// pure front-end display switch — stored content (question text, rubrics, AI output) is
    /// unaffected and may still be Chinese whatever this is set to.
    /// </summary>
    public partial class AddUserLanguagePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language_preference",
                table: "users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language_preference",
                table: "users");
        }
    }
}
