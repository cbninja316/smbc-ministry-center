using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptIsDone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDone",
                table: "Receipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDone",
                table: "Receipts");
        }
    }
}
