using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChildCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInToken",
                table: "Children",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Children",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Children",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedByUserId",
                table: "Children",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChildId = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckedOutAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckedInByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedOutByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsManual = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildCheckIns_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildCheckIns_Users_CheckedInByUserId",
                        column: x => x.CheckedInByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildCheckIns_Users_CheckedOutByUserId",
                        column: x => x.CheckedOutByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Children_CheckInToken",
                table: "Children",
                column: "CheckInToken",
                unique: true,
                filter: "[CheckInToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Children_VerifiedByUserId",
                table: "Children",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildCheckIns_CheckedInByUserId",
                table: "ChildCheckIns",
                column: "CheckedInByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildCheckIns_CheckedOutByUserId",
                table: "ChildCheckIns",
                column: "CheckedOutByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildCheckIns_ChildId",
                table: "ChildCheckIns",
                column: "ChildId");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_Users_VerifiedByUserId",
                table: "Children",
                column: "VerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_Users_VerifiedByUserId",
                table: "Children");

            migrationBuilder.DropTable(
                name: "ChildCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_Children_CheckInToken",
                table: "Children");

            migrationBuilder.DropIndex(
                name: "IX_Children_VerifiedByUserId",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "CheckInToken",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "Children");
        }
    }
}
