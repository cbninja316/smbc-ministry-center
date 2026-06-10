using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSpecialEventScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecialEventId",
                table: "VolunteerRoles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoles_SpecialEventId",
                table: "VolunteerRoles",
                column: "SpecialEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_VolunteerRoles_SpecialEvents_SpecialEventId",
                table: "VolunteerRoles",
                column: "SpecialEventId",
                principalTable: "SpecialEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VolunteerRoles_SpecialEvents_SpecialEventId",
                table: "VolunteerRoles");

            migrationBuilder.DropIndex(
                name: "IX_VolunteerRoles_SpecialEventId",
                table: "VolunteerRoles");

            migrationBuilder.DropColumn(
                name: "SpecialEventId",
                table: "VolunteerRoles");
        }
    }
}
