using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleTimeSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleTimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Time = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleTimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleTimeSlots_VolunteerRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "VolunteerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleTimeSlots_RoleId",
                table: "RoleTimeSlots",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleTimeSlots");
        }
    }
}
