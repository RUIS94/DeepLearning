using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabLiteralTranslatableAndCanonicalKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "canonical_key",
                table: "vocab_expressions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "literal_translatable",
                table: "vocab_expressions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "canonical_key",
                table: "sentence_patterns",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_vocab_canonical",
                table: "vocab_expressions",
                column: "canonical_key");

            migrationBuilder.CreateIndex(
                name: "idx_pattern_canonical",
                table: "sentence_patterns",
                column: "canonical_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_vocab_canonical",
                table: "vocab_expressions");

            migrationBuilder.DropIndex(
                name: "idx_pattern_canonical",
                table: "sentence_patterns");

            migrationBuilder.DropColumn(
                name: "canonical_key",
                table: "vocab_expressions");

            migrationBuilder.DropColumn(
                name: "literal_translatable",
                table: "vocab_expressions");

            migrationBuilder.DropColumn(
                name: "canonical_key",
                table: "sentence_patterns");
        }
    }
}
