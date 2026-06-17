using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtBudgetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BudgetCategoryId",
                table: "Debts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebtId",
                table: "BudgetEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Debts_BudgetCategoryId",
                table: "Debts",
                column: "BudgetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetEntries_DebtId",
                table: "BudgetEntries",
                column: "DebtId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Debts_DebtId",
                table: "BudgetEntries",
                column: "DebtId",
                principalTable: "Debts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts",
                column: "BudgetCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Debts_DebtId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_BudgetCategoryId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_BudgetEntries_DebtId",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "BudgetCategoryId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "DebtId",
                table: "BudgetEntries");
        }
    }
}
