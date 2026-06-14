using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryDonateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SalaryDonateEnabled",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SalaryDonateGivingCategoryId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryDonatePercentage",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SalaryDonateGivingCategoryId",
                table: "Users",
                column: "SalaryDonateGivingCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users",
                column: "SalaryDonateGivingCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_BudgetCategories_SalaryDonateGivingCategoryId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SalaryDonateGivingCategoryId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SalaryDonateEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SalaryDonateGivingCategoryId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SalaryDonatePercentage",
                table: "Users");
        }
    }
}
