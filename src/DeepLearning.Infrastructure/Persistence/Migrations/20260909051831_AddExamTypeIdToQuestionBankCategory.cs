using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeepLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamTypeIdToQuestionBankCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exam_type_id",
                table: "question_bank_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_bank_categories_exam_type_id",
                table: "question_bank_categories",
                column: "exam_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_question_bank_categories_exam_types_exam_type_id",
                table: "question_bank_categories",
                column: "exam_type_id",
                principalTable: "exam_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_question_bank_categories_exam_types_exam_type_id",
                table: "question_bank_categories");

            migrationBuilder.DropIndex(
                name: "ix_question_bank_categories_exam_type_id",
                table: "question_bank_categories");

            migrationBuilder.DropColumn(
                name: "exam_type_id",
                table: "question_bank_categories");
        }
    }
}
