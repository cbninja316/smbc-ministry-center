using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorshipRoleLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorshipServiceTypeId",
                table: "VolunteerRoles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoles_WorshipServiceTypeId",
                table: "VolunteerRoles",
                column: "WorshipServiceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_VolunteerRoles_WorshipServiceTypes_WorshipServiceTypeId",
                table: "VolunteerRoles",
                column: "WorshipServiceTypeId",
                principalTable: "WorshipServiceTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VolunteerRoles_WorshipServiceTypes_WorshipServiceTypeId",
                table: "VolunteerRoles");

            migrationBuilder.DropIndex(
                name: "IX_VolunteerRoles_WorshipServiceTypeId",
                table: "VolunteerRoles");

            migrationBuilder.DropColumn(
                name: "WorshipServiceTypeId",
                table: "VolunteerRoles");
        }
    }
}
