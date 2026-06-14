using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSalary",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SalaryUserId",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_SalaryUserId",
                table: "BudgetCategories",
                column: "SalaryUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetCategories_Users_SalaryUserId",
                table: "BudgetCategories",
                column: "SalaryUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetCategories_Users_SalaryUserId",
                table: "BudgetCategories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetCategories_SalaryUserId",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "IsSalary",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "SalaryUserId",
                table: "BudgetCategories");
        }
    }
}
