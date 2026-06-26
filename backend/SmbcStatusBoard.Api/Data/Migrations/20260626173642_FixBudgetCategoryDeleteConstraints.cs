using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixBudgetCategoryDeleteConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts",
                column: "BudgetCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users",
                column: "SalaryDonateGivingCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_BudgetCategories_BudgetCategoryId",
                table: "Debts",
                column: "BudgetCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users",
                column: "SalaryDonateGivingCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id");
        }
    }
}
