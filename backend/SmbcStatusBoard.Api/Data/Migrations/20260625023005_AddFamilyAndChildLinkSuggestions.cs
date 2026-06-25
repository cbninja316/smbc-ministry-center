using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyAndChildLinkSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpouseUserId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentUserId",
                table: "Children",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildLinkSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestingUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    NewChildId = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestedChildId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildLinkSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildLinkSuggestions_Children_NewChildId",
                        column: x => x.NewChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildLinkSuggestions_Children_SuggestedChildId",
                        column: x => x.SuggestedChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildLinkSuggestions_Users_RequestingUserId",
                        column: x => x.RequestingUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SpouseUserId",
                table: "Users",
                column: "SpouseUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Children_ParentUserId",
                table: "Children",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildLinkSuggestions_NewChildId",
                table: "ChildLinkSuggestions",
                column: "NewChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildLinkSuggestions_RequestingUserId",
                table: "ChildLinkSuggestions",
                column: "RequestingUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildLinkSuggestions_SuggestedChildId",
                table: "ChildLinkSuggestions",
                column: "SuggestedChildId");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_Users_ParentUserId",
                table: "Children",
                column: "ParentUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_SpouseUserId",
                table: "Users",
                column: "SpouseUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_Users_ParentUserId",
                table: "Children");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_SpouseUserId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ChildLinkSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_Users_SpouseUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Children_ParentUserId",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "SpouseUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentUserId",
                table: "Children");
        }
    }
}
