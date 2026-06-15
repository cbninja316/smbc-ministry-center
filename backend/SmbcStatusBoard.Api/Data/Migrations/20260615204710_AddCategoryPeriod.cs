using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodEndDay",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodEndMonth",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodStartDay",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodStartMonth",
                table: "BudgetCategories",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodEndDay",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "PeriodEndMonth",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "PeriodStartDay",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "PeriodStartMonth",
                table: "BudgetCategories");
        }
    }
}
