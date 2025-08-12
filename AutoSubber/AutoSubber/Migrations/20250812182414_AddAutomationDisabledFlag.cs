using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSubber.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationDisabledFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomationDisabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomationDisabled",
                table: "AspNetUsers");
        }
    }
}
