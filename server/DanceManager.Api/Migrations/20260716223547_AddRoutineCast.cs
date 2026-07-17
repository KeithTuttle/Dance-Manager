using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutineCast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutineCasts",
                columns: table => new
                {
                    RoutineId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineCasts", x => new { x.RoutineId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_RoutineCasts_Routines_RoutineId",
                        column: x => x.RoutineId,
                        principalTable: "Routines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutineCasts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutineCasts_StudentId",
                table: "RoutineCasts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutineCasts_TenantId",
                table: "RoutineCasts",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutineCasts");
        }
    }
}
