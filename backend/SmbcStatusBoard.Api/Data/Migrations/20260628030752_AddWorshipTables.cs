using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmbcStatusBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorshipTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorshipServiceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorshipSongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    PraiseChartsId = table.Column<string>(type: "TEXT", nullable: true),
                    PraiseChartsSlug = table.Column<string>(type: "TEXT", nullable: true),
                    PraiseChartsThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipSongs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorshipPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlanDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorshipPlans_WorshipServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "WorshipServiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorshipPlanSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipPlanSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorshipPlanSections_WorshipPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "WorshipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorshipPlanItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SongId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventTitle = table.Column<string>(type: "TEXT", nullable: true),
                    LeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorshipPlanItems_WorshipPlanSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "WorshipPlanSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorshipPlanItems_WorshipSongs_SongId",
                        column: x => x.SongId,
                        principalTable: "WorshipSongs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorshipPlanItems_SectionId",
                table: "WorshipPlanItems",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorshipPlanItems_SongId",
                table: "WorshipPlanItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_WorshipPlans_ServiceTypeId",
                table: "WorshipPlans",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorshipPlanSections_PlanId",
                table: "WorshipPlanSections",
                column: "PlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorshipPlanItems");

            migrationBuilder.DropTable(
                name: "WorshipPlanSections");

            migrationBuilder.DropTable(
                name: "WorshipSongs");

            migrationBuilder.DropTable(
                name: "WorshipPlans");

            migrationBuilder.DropTable(
                name: "WorshipServiceTypes");
        }
    }
}
