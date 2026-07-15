using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShowSectionsAndStandaloneEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RoutineId",
                table: "ShowPrograms",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "ShowPrograms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudioId",
                table: "ShowPrograms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ShowPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShowSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StudioId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowSections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShowPrograms_SectionId",
                table: "ShowPrograms",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowSections_TenantId",
                table: "ShowSections",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShowPrograms_ShowSections_SectionId",
                table: "ShowPrograms",
                column: "SectionId",
                principalTable: "ShowSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill StudioId on existing (routine-linked) rows from routine -> class,
            // so the studio-scoped GET keeps returning them after it switches to
            // filtering on StudioId directly.
            migrationBuilder.Sql(@"
                UPDATE ""ShowPrograms"" sp SET ""StudioId"" = c.""StudioId""
                FROM ""Routines"" r
                JOIN ""Classes"" c ON c.""Id"" = r.""ClassId""
                WHERE sp.""RoutineId"" = r.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShowPrograms_ShowSections_SectionId",
                table: "ShowPrograms");

            migrationBuilder.DropTable(
                name: "ShowSections");

            migrationBuilder.DropIndex(
                name: "IX_ShowPrograms_SectionId",
                table: "ShowPrograms");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "ShowPrograms");

            migrationBuilder.DropColumn(
                name: "StudioId",
                table: "ShowPrograms");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ShowPrograms");

            migrationBuilder.AlterColumn<int>(
                name: "RoutineId",
                table: "ShowPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
