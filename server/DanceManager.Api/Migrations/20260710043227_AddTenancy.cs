using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Studios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StudentNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StudentMilestoneStatuses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SongChoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ShowPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Routines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RecitalParticipations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Milestones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LessonPlanEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Formations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CostumeRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CostumeOptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ClassSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Classes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Auditions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditionCandidates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AttendanceRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ClerkUserId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Studios_TenantId",
                table: "Studios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId",
                table: "Students",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentNotes_TenantId",
                table: "StudentNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMilestoneStatuses_TenantId",
                table: "StudentMilestoneStatuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SongChoices_TenantId",
                table: "SongChoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowPrograms_TenantId",
                table: "ShowPrograms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Routines_TenantId",
                table: "Routines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RecitalParticipations_TenantId",
                table: "RecitalParticipations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_TenantId",
                table: "Milestones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlanEntries_TenantId",
                table: "LessonPlanEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Formations_TenantId",
                table: "Formations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeRecords_TenantId",
                table: "CostumeRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeOptions_TenantId",
                table: "CostumeOptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_TenantId",
                table: "ClassSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_TenantId",
                table: "Classes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditions_TenantId",
                table: "Auditions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditionCandidates_TenantId",
                table: "AuditionCandidates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId",
                table: "AttendanceRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ClerkUserId",
                table: "Memberships",
                column: "ClerkUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId",
                table: "Memberships",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Studios_TenantId",
                table: "Studios");

            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_StudentNotes_TenantId",
                table: "StudentNotes");

            migrationBuilder.DropIndex(
                name: "IX_StudentMilestoneStatuses_TenantId",
                table: "StudentMilestoneStatuses");

            migrationBuilder.DropIndex(
                name: "IX_SongChoices_TenantId",
                table: "SongChoices");

            migrationBuilder.DropIndex(
                name: "IX_ShowPrograms_TenantId",
                table: "ShowPrograms");

            migrationBuilder.DropIndex(
                name: "IX_Routines_TenantId",
                table: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_RecitalParticipations_TenantId",
                table: "RecitalParticipations");

            migrationBuilder.DropIndex(
                name: "IX_Milestones_TenantId",
                table: "Milestones");

            migrationBuilder.DropIndex(
                name: "IX_LessonPlanEntries_TenantId",
                table: "LessonPlanEntries");

            migrationBuilder.DropIndex(
                name: "IX_Formations_TenantId",
                table: "Formations");

            migrationBuilder.DropIndex(
                name: "IX_CostumeRecords_TenantId",
                table: "CostumeRecords");

            migrationBuilder.DropIndex(
                name: "IX_CostumeOptions_TenantId",
                table: "CostumeOptions");

            migrationBuilder.DropIndex(
                name: "IX_ClassSessions_TenantId",
                table: "ClassSessions");

            migrationBuilder.DropIndex(
                name: "IX_Classes_TenantId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Auditions_TenantId",
                table: "Auditions");

            migrationBuilder.DropIndex(
                name: "IX_AuditionCandidates_TenantId",
                table: "AuditionCandidates");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_TenantId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StudentNotes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StudentMilestoneStatuses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SongChoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ShowPrograms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RecitalParticipations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LessonPlanEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Formations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CostumeRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CostumeOptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Auditions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditionCandidates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AttendanceRecords");
        }
    }
}
