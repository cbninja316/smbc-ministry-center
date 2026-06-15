using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmittedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmittedByUserId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_SubmittedByUserId",
                table: "Items",
                column: "SubmittedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Users_SubmittedByUserId",
                table: "Items",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Users_SubmittedByUserId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SubmittedByUserId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Items");
        }
    }
}
