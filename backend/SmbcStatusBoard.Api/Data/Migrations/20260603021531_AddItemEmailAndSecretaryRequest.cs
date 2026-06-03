using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItemEmailAndSecretaryRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Items",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Items");
        }
    }
}
