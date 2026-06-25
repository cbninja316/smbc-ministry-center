using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChildLinkedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedUserId",
                table: "Children",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Children_LinkedUserId",
                table: "Children",
                column: "LinkedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_Users_LinkedUserId",
                table: "Children",
                column: "LinkedUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_Users_LinkedUserId",
                table: "Children");

            migrationBuilder.DropIndex(
                name: "IX_Children_LinkedUserId",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "LinkedUserId",
                table: "Children");
        }
    }
}
