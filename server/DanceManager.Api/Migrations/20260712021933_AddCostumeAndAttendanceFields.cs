using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCostumeAndAttendanceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Accessories",
                table: "CostumeOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shoes",
                table: "CostumeOptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accessories",
                table: "CostumeOptions");

            migrationBuilder.DropColumn(
                name: "Shoes",
                table: "CostumeOptions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AttendanceRecords");
        }
    }
}
