using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChurches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChurchId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Churches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LogoData = table.Column<string>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Churches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ChurchId",
                table: "Users",
                column: "ChurchId");

            // Seed Southmoore Baptist Church as church #1
            migrationBuilder.InsertData(
                table: "Churches",
                columns: new[] { "Id", "Name", "Slug", "LogoData", "CreatedAt" },
                values: new object[] { 1, "Southmoore Baptist Church", "southmoore-baptist-church", null!, DateTime.UtcNow });

            // Assign all existing users to church #1
            migrationBuilder.Sql("UPDATE Users SET ChurchId = 1 WHERE ChurchId IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Churches_ChurchId",
                table: "Users",
                column: "ChurchId",
                principalTable: "Churches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Churches_ChurchId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Churches");

            migrationBuilder.DropIndex(
                name: "IX_Users_ChurchId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ChurchId",
                table: "Users");
        }
    }
}
