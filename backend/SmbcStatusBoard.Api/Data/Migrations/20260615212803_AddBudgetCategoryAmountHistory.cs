using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetCategoryAmountHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetCategoryAmountHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BudgetCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategoryAmountHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetCategoryAmountHistories_BudgetCategories_BudgetCategoryId",
                        column: x => x.BudgetCategoryId,
                        principalTable: "BudgetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategoryAmountHistories_BudgetCategoryId_Year_Month",
                table: "BudgetCategoryAmountHistories",
                columns: new[] { "BudgetCategoryId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetCategoryAmountHistories");
        }
    }
}
