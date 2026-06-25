using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventRegistrationChildCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventRegistrations_Children_ChildId",
                table: "EventRegistrations");

            migrationBuilder.AddForeignKey(
                name: "FK_EventRegistrations_Children_ChildId",
                table: "EventRegistrations",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventRegistrations_Children_ChildId",
                table: "EventRegistrations");

            migrationBuilder.AddForeignKey(
                name: "FK_EventRegistrations_Children_ChildId",
                table: "EventRegistrations",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "Id");
        }
    }
}
